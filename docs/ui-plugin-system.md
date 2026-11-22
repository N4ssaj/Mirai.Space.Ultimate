# UI Plugin System — Specification

| Field | Value |
| --- | --- |
| Status | **Draft / proposal** — nothing implemented, design not approved |
| Date | 2026-11-21 |
| Scope | Avalonia client (`Mirai.Space.UI` + hosts) |
| Decision level | Architecture; see §19 for the ADR-lite entries to append to `DECISIONS.md` |
| Related | `docs/avalonia-host-and-reactiveui.md`, `DECISIONS.md`, `AGENTS.md` |

This document is the input for a design review. It states the proposed model, the
contract, the loading semantics and the phasing. Code in this document is a **sketch**
of signatures, not verified compiling code.

---

## 1. Context

`Mirai.Space.UI` is currently a blank shell: `App` creates `MainWindow`, injects
`MainWindowViewModel`, `MainView` renders a single `TextBox`. There is no plugin
infrastructure anywhere in the solution (`grep -i plugin` over `*.cs` returns nothing),
and no shell regions yet.

At the same time the product needs third-party UI extension: extra documents, tools,
menu commands, settings pages. The two constraints that shape everything below come from
the existing host design:

1. **The host is a composition root only.** Per `DECISIONS.md` (2026-11-21) every client
   project exposes a flat `Add<Layer>()` and never chains another layer; the host wires
   them. `App` creates the window and assigns `DataContext`; views keep parameterless
   constructors and never assign `DataContext` themselves.
2. **There are two hosts with different physics.** Desktop is `net10.0`, `WinExe`,
   full `IHost`; Browser is `net10.0-browser` (WebAssembly) and physically cannot load
   assemblies from disk at runtime. Any design that assumes "download a dll" breaks
   Browser.

## 2. Goals

- Plugins extend the UI **without modifying the shell** and **without the shell knowing
  about plugins**.
- One extension contract used by first-party features and third-party plugins alike.
- A broken, slow or incompatible plugin must not prevent the app from starting.
- Plugins must be installable/removable as a folder, with a manifest that can be
  inspected **before** its code is loaded.
- The plugin API must be small enough to keep stable for years, and declarative enough
  that a future out-of-process or WASM host could implement the same contract.
- Browser must keep working, using the same contract in a static (compile-time) mode.

## 3. Non-goals (v1)

- Hot reload / in-place plugin update without restart. Install, update, disable and
  uninstall all take effect on the next start.
- Sandboxing or permission model. In-process plugins run with full trust; see §10.
- Plugin dependency resolution between plugins (A requires B). Manifest carries the
  field, the loader refuses to load a plugin whose declared plugin dependencies are
  missing, but there is no version solver.
- Server-side plugins, marketplace, paid plugins, telemetry.
- Visual designer or drag-and-drop composition of contributions.

## 4. Prior art and what we take from it

| Project | What it proves | What we take | What we reject |
| --- | --- | --- | --- |
| [MS Learn — app with plugin support](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support) | `AssemblyLoadContext` + `AssemblyDependencyResolver` is the supported way | Per-plugin ALC, `EnableDynamicLoading`, contract refs as `Private=false`/`ExcludeAssets=runtime` | Its `IPlugin` sample is a toy surface |
| [DotNetCorePlugins](https://github.com/natemcmaster/DotNetCorePlugins) | Shared-type unification across ALC works in production | Shared contract list idea, `EnterContextualReflection` awareness | Its default fallback to the host assembly context hides version skew |
| [ShareX/XerahS](https://github.com/ShareX/XerahS/blob/develop/.ai/skills/feature-specifications/SKILL.md) | Avalonia app with `plugin.json`, `Plugins/` folder, static loader root | Manifest, static root of loaded assemblies, "Avalonia comes from host only" | Loading plugin XAML resources globally |
| [FramePFX](https://github.com/AngryCarrot789/FramePFX/blob/master/docs/Plugins.md) | Core modules vs dynamic plugins, explicit lifecycle, typed contribution points | Two-tier model, ordered lifecycle, contribution points instead of "here is the window" | Its `RegisterXamlResources` global merge |
| [Prism modularity](https://docs.prismlibrary.com/docs/9.0/modularity) | `RegisterTypes` then `OnInitialized`, catalog discovery, dependency/cycle checks | Two-phase init, failure isolation per module | Prism as a dependency; loaded modules can never unload |
| [OrchardCore manifest](https://docs.orchardcore.net/projects/O1/en/latest/Documentation/Manifest-files) | `Module.txt` with features, dependencies, priority | Manifest-first discovery, feature-level enable/disable | Server-oriented feature model |
| [asv-soft/asv-drones](https://github.com/asv-soft/asv-drones) | Real Avalonia GCS with plugins from `plugins/`, assembly prefix convention | Folder-per-plugin, prefix convention as a hint | Prefix-only discovery (no manifest) |
| [mbdev87/DockTemplate](https://github.com/mbdev87/DockTemplate) | `.dockplugin` packaged files, drop-to-install, hot-load | Single-file package, drop/install UX | Hot-load in v1 |
| [joesdu/VelaShell](https://github.com/joesdu/VelaShell) | Dual mode: in-process and separate process, manifest declares the mode | Manifest field for execution mode (reserved, not used in v1) | IPC for v1 |
| [.NET unloadability](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability) | Collectible ALC unload is cooperative and leak-prone | Collectible ALC + unload tests, but restart as the product story | Promising reload |

Why not `System.Composition`/MEF: `[Export]`/`[Import]` resolution is implicit, failures
surface as missing composition rather than a load error, there is no manifest
pre-validation, and MEF holds strong references that make unload impossible. Why not
Prism: it would add a second application framework (regions, its own DI story) on top of
our host rules for one feature.

## 5. Architecture

### 5.1 Two tiers, one contract

```
┌─────────────────────────────── host (Desktop / Browser) ───────────────────────────────┐
│  Program / App                                                                         │
│     │                                                                                  │
│     ▼                                                                                  │
│  IPluginHost  ── StaticPluginSource   (compiled-in modules; works in Browser)          │
│     │         └ DirectoryPluginSource (plugins/<id>/plugin.json; Desktop only)         │
│     ▼                                                                                  │
│  PluginLoader ── manifests → validation → per-plugin ALC → child IServiceProvider      │
│     │                                                                                  │
│     ▼                                                                                  │
│  IContributionRegistry (observable)  ◄── shell (MainView) renders regions              │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Tier A — modules.** First-party features (and anything needed in Browser) implement
  `IPlugin` and are registered statically. They get a host-backed context, not a child
  container: they are trusted and may use host services directly.
- **Tier B — plugins.** Third-party assemblies, folder-per-plugin, loaded into a
  collectible `AssemblyLoadContext`, services in a child container.

Both tiers register into the same `IContributionRegistry`, so the shell has exactly one
code path. Browser ships Tier A only; this is why the shell must never reference the
directory loader.

### 5.2 Projects and dependency rules

| Project | TFM | References | Purpose |
| --- | --- | --- | --- |
| `Mirai.Space.Plugins.Abstractions` | `net10.0`, plus `net10.0-browser` if the shell types need it | `Avalonia`, `DynamicData`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions` | **Plugin SDK**: `IPlugin`, contexts, manifest, contribution descriptors, registry, storage. No host project, no loader. |
| `Mirai.Space.Plugins` | `net10.0` (desktop only, **never referenced by `UI` or `Browser`**) | `Plugins.Abstractions` | Manifest reader, validation, ALC, child provider, install/uninstall, state store. |
| `Mirai.Space.Plugins.Tests` | `net10.0` | `Plugins`, `Plugins.Abstractions`, test infrastructure | Unit + integration tests, sample plugin fixtures. |
| `samples/Mirai.Space.Plugin.Sample` | `net10.0` | `Plugins.Abstractions` only | Reference plugin; not referenced by any host project. |
| `samples/Mirai.Space.Plugin.Broken` | `net10.0` | `Plugins.Abstractions` | Failure-path fixture (throws in init / bad manifest / missing dep). |

Rules enforced by review (and later by architecture tests):

- `Plugins.Abstractions` must **not** reference `Mirai.Space.UI`, `Presentation`,
  `Platform`, `Realtime`, `Request`, or `Plugins`.
- `Mirai.Space.UI` references `Plugins.Abstractions` only; it must not reference the
  loader project. `App` starts `IPluginHost`, it does not know about `AssemblyLoadContext`.
- Hosts (Desktop/Browser) are the only projects that call `AddPlugins(...)` /
  `AddDynamicPlugins(...)`.
- Contract assemblies are referenced by plugins with `ExcludeAssets="runtime"` and
  `Private="false"`, never copied into the plugin output.

### 5.3 Runtime object graph

```
PluginHost (singleton)
 ├─ PluginOptions (paths, timeouts, shared-assembly policy)
 ├─ PluginStateStore (plugins.state.json: enabled, installed version, source)
 ├─ StaticPluginSource ── IPlugin instances resolved from host DI
 ├─ DirectoryPluginSource ── PluginLoadContext[plugin] + PluginServiceProvider[plugin]
 └─ IContributionRegistry (singleton, DynamicData source; owned by Abstractions)
```

A `PluginDescriptor` is the single mutable state object per plugin:

```csharp
public enum PluginState { Discovered, Incompatible, Disabled, Loading, Ready, Failed }

public sealed class PluginDescriptor
{
    public required PluginManifest Manifest { get; init; }
    public required PluginSource Source { get; init; }      // Bundled | User | Static
    public required string Location { get; init; }          // directory or "static"
    public PluginState State { get; set; }
    public string? Error { get; set; }
    public TimeSpan LoadTime { get; set; }
    internal IPlugin? Instance { get; set; }
    internal PluginLoadContext? LoadContext { get; set; }
    internal IServiceProvider? Services { get; set; }
}
```

## 6. Plugin contract

### 6.1 Lifecycle

```csharp
namespace Mirai.Space.Plugins;

public interface IPlugin
{
    ValueTask ConfigureAsync(IPluginConfigurationContext context, CancellationToken ct);
    ValueTask InitializeAsync(IPluginContext context, CancellationToken ct);
    ValueTask ShutdownAsync(CancellationToken ct);
}
```

- `ConfigureAsync` — register services and options. Registered into the plugin's own
  `IServiceCollection`; no UI, no I/O beyond reading its own manifest.
- `InitializeAsync` — register contributions, read storage, start background work.
  Runs on the UI thread (§7.4).
- `ShutdownAsync` — cancel work, release resources. Contributions are removed by the
  host before this is called, so the plugin never has to clean the registry itself.

Mirrors Prism's `RegisterTypes`/`OnInitialized` split and FramePFX's ordered lifecycle,
but with only three steps.

### 6.2 Contexts

```csharp
public interface IPluginConfigurationContext
{
    PluginManifest Manifest { get; }
    IServiceCollection Services { get; }
    ILogger Logger { get; }
}

public interface IPluginContext
{
    PluginManifest Manifest { get; }
    IServiceProvider Services { get; }          // plugin container, host fallback
    IContributionRegistry Contributions { get; }
    IPluginStorage Storage { get; }
    ILogger Logger { get; }
    CancellationToken Stopping { get; }         // cancelled before ShutdownAsync
}
```

`Services` resolves: plugin registrations first, then host. A plugin that needs a host
scoped service (`DbContext`, a session) resolves it here; the host scope lives as long
as the plugin (§7.3).

### 6.3 Manifest

`plugin.json` next to the entry assembly, required, UTF-8:

```json
{
  "schemaVersion": 1,
  "id": "com.acme.notes",
  "name": "Notes",
  "version": "1.2.0",
  "apiVersion": "1.0",
  "entryAssembly": "Acme.Notes.dll",
  "entryType": "Acme.Notes.NotesPlugin",
  "minHostVersion": "0.1.0",
  "platforms": ["desktop"],
  "description": "Markdown notes in the workspace.",
  "author": "Acme",
  "homepage": "https://acme.example/notes",
  "icon": "avares://Acme.Notes/Assets/icon-32.png"
}
```

| Field | Required | Rule |
| --- | --- | --- |
| `schemaVersion` | yes | currently `1`; unknown value → `Incompatible` |
| `id` | yes | `^[a-z][a-z0-9]*(\.[a-z0-9-]+)+$`, globally unique, immutable across versions |
| `name` | yes | display name, ≤ 64 chars |
| `version` | yes | SemVer 2.0 (`1.2.0`, `1.2.0-beta.1`); parsed by a small internal parser (no new package) |
| `apiVersion` | yes | `<major>.<minor>`; `major` must equal `PluginApi.Current.Major` |
| `entryAssembly` | yes | file name only, no separators, must exist inside the plugin folder; `.dll` |
| `entryType` | yes | assembly-qualified type name; must implement `IPlugin` |
| `minHostVersion` | no | if host version is lower → `Incompatible` |
| `platforms` | no | subset of `desktop`, `browser`, `android`, `ios`; default `desktop` |
| `icon`, `description`, `author`, `homepage` | no | presentation only |

Validation happens **before** any assembly is loaded, so a malformed or incompatible
plugin can be listed in the plugin manager and never executed. The manifest is metadata,
not a security boundary.

`PluginManifest` is a `record` deserialized with a `JsonSerializerContext` (source-gen),
read with `FileOptions.Asynchronous`, max 64 KiB.

### 6.4 Contribution points (v1)

```csharp
public enum ShellRegion { Document, LeftTool, RightTool, BottomTool, MainMenu, Settings, StatusBar }

public sealed record PluginView(object ViewModel, Control View);
```

| Descriptor | Purpose |
| --- | --- |
| `DocumentContribution<TViewModel>` | a document tab: `Id`, `Title`, `IconKey`, `Order`, `Func<IServiceProvider, TViewModel>`, `Func<TViewModel, Control>` |
| `ToolPanelContribution` | a dockable panel in `LeftTool`/`RightTool`/`BottomTool` |
| `MenuContribution` | item under a menu path (`"File"`, `"View/Tools"`), carries `ICommand` and optional `KeyGesture` |
| `SettingsContribution` | a section on the settings page |
| `StatusBarContribution` | a text/icon slot (deferred to v1.1 if the shell is not ready) |

Deliberately excluded in v1: context menus on arbitrary controls, property-editor slots,
global styles/themes, notifications, custom windows, toolbar buttons outside menus.
Those are additive later without breaking the surface.

The registry wraps every descriptor with its owning plugin id:

```csharp
public sealed record PluginContribution(string PluginId, IContribution Descriptor);

public interface IContributionRegistry
{
    ISourceList<PluginContribution> Items { get; }   // shell binds via .Connect()
    void Add(string pluginId, IContribution contribution);
    void RemovePlugin(string pluginId);
}
```

The shell never mutates `Items`; it projects them into region collections
(§8.3). Removal is by `pluginId`, so a plugin cannot remove another plugin's items.

### 6.5 View materialization

The host, not the plugin, owns `DataContext` (house rule). Contribution factories return
the pair and the shell assigns it:

```csharp
var view = contribution.CreateView(services);
if (view.View.DataContext is null)
    view.View.DataContext = view.ViewModel;
```

No `ViewLocator`, no naming convention, no reflection over plugin assemblies. A
convention-based locator cannot work across plugin assemblies without loading every
type, and it silently produces empty views when the convention is missed.

### 6.6 Storage

```csharp
public interface IPluginStorage
{
    string RootPath { get; }
    ValueTask<T?> GetAsync<T>(string key, CancellationToken ct);
    ValueTask SetAsync<T>(string key, T value, CancellationToken ct);
    ValueTask RemoveAsync(string key, CancellationToken ct);
}
```

- Root: `%LOCALAPPDATA%\Mirai.Space\plugin-data\<id>\` (Linux `~/.local/share/...`,
  macOS `~/Library/Application Support/...`), created lazily.
- JSON files per key, atomic write (temp + `File.Move(overwrite: true)`), source-gen
  serializer, 1 MiB per key cap.
- Browser: in-memory or `localStorage` implementation registered by the Browser host.
- Data survives version upgrades; `Storage.SchemaVersion` is the plugin's problem.

### 6.7 Logging

`ILogger` is a host child logger with a `PluginId` property, so Serilog output is
filterable per plugin. Plugins must not configure logging themselves.

## 7. Host: discovery, loading, isolation

### 7.1 Pipeline

| Phase | Where | What happens | On failure |
| --- | --- | --- | --- |
| Discover | UI thread, async I/O | Enumerate `<app>/plugins/*` and `%LOCALAPPDATA%\Mirai.Space\plugins/*`; user folder shadows bundled folder for the same id | Folder skipped, logged |
| Validate | UI thread | Parse manifest, SemVer/apiVersion/platform, path safety, duplicate id | `Incompatible`, no assembly load |
| Load | UI thread | `PluginLoadContext`, entry assembly, `ActivatorUtilities.CreateInstance(services, entryType)` | `Failed` |
| Configure | UI thread | `ConfigureAsync` → `ServiceCollection` → child provider built | `Failed`, provider disposed |
| Initialize | UI thread | `InitializeAsync` → contributions into the registry | `Failed`, contributions rolled back |
| Ready | — | Listed in the plugin manager | — |

Shutdown is the reverse order, per plugin, in reverse load order.

### 7.2 Assembly load context policy

```csharp
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    // Host copies win for the whole UI stack: two Avalonia copies mean duplicate
    // control types, duplicated AvaloniaLocator, and InvalidCastException at runtime.
    private static readonly string[] SharedPrefixes =
        ["Avalonia", "ReactiveUI", "Splat", "DynamicData", "System.Reactive",
         "Microsoft.Extensions.", "System.", "netstandard"];
    // True contract assemblies are loaded once, from the default context.
    private static readonly string[] SharedContracts = ["Mirai.Space.Plugins.Abstractions"];

    private readonly AssemblyDependencyResolver _resolver;

    protected override Assembly? Load(AssemblyName name)
    {
        if (SharedContracts.Contains(name.Name)) return null;       // default context
        if (SharedPrefixes.Any(p => name.Name!.StartsWith(p, StringComparison.Ordinal))) return null; // host copy
        var path = _resolver.ResolveAssemblyToPath(name)
            ?? throw new FileNotFoundException($"Plugin assembly '{name}' not found in plugin folder");
        return LoadFromAssemblyPath(path);
    }
}
```

Policy consequences, to be stated to plugin authors:

- The plugin must compile against the **host's** package versions (shared CPM props
  imported from the SDK), because it will run against the host copy of Avalonia,
  ReactiveUI and `Microsoft.Extensions.*`.
- Everything else — the plugin's own dependencies — must be inside its folder
  (`EnableDynamicLoading` copies them) and is loaded in the plugin ALC.
- No silent fallback to the host context: a missing dependency fails loudly with the
  plugin id in the message.
- Native dependencies resolve through `AssemblyDependencyResolver`; `LoadUnmanagedDll`
  delegates to it.
- One ALC per plugin, `isCollectible: true`. The `PluginDescriptor` roots the ALC; a
  static `RootAssemblies` list is not needed because the host owns the descriptors.
- The host must not be published trimmed or AOT while the plugin loader ships
  (`PublishTrimmed=false`); trimming is a separate decision.

### 7.3 DI model

Plugins do **not** register into the host container. Reasons: a failing plugin must not
break `Build()`/`ValidateOnBuild`, plugin types must not collide with host types, and
unload requires the container to be disposable per plugin.

```csharp
public sealed class PluginServiceProvider : IServiceProvider, IServiceProviderIsService, IAsyncDisposable
{
    private readonly ServiceProvider _own;     // plugin's ServiceCollection
    private readonly IServiceScope _hostScope; // host scope spanning the plugin lifetime
}
```

- Resolution order: plugin container → host scope. Implements `IServiceProviderIsService`
  so `ActivatorUtilities` picks the right constructor.
- `IServiceScopeFactory` creates plugin-owned scopes whose provider still falls back to
  the host scope.
- The host scope is created once per plugin, so host-scoped services (`DbContext`,
  session state) behave correctly and are never captured from the root provider.
- Rule for host code: **the host never resolves plugin services.** Communication happens
  through contributions only. This keeps plugin service graphs unloadable and the host
  dependency direction one-way.

Static modules (Tier A) skip this and use the host provider directly.

### 7.4 Startup orchestration and threading

`App` builds the window first, then starts the plugin host, so a slow plugin never delays
the first frame:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    var mainWindow = new MainWindow { DataContext = _mainWindowViewModel };
    // ... assign to desktop lifetime ...
    base.OnFrameworkInitializationCompleted();
    _ = StartPluginsAsync();          // not awaited; failures are logged and surfaced
}

private async Task StartPluginsAsync()
{
    try { await _pluginHost.StartAsync(_shutdown.Token); }
    catch (Exception ex) { _logger.LogError(ex, "Plugin host failed to start"); }
}
```

- `StartAsync` is called on the UI thread and keeps the Avalonia `SynchronizationContext`
  for the whole pipeline (no `ConfigureAwait(false)` inside the loader), so
  `InitializeAsync` can touch `Application.Current` and create controls.
- Between plugins the loader yields (`await Task.Yield()`) so the UI stays responsive.
- Per-plugin timeout: manifest read 2 s, `InitializeAsync` 15 s (both configurable). A
  timeout ⇒ `Failed` with a message; the plugin's contributions are removed.
- Plugins doing heavy work start their own background tasks in `InitializeAsync` and
  report progress by mutating their own view models.

### 7.5 Failure isolation

Every phase is wrapped per plugin. A failure:

1. sets `State = Failed` and `Error`;
2. rolls back contributions added by that plugin;
3. disposes its provider and, if collectible-unload is safe, requests ALC unload;
4. is logged with `PluginId` and shown in the plugin manager.

Nothing from a failed plugin ever reaches the shell. The app does not show a dialog at
startup; a status-bar badge opens the plugin manager instead.

### 7.6 Unload policy

- ALC is collectible from day one, and tests assert that a loaded-then-shutdown plugin's
  ALC is actually collected (weak reference + forced GC). This keeps the design honest.
- Product behaviour in v1: **install, update, enable, disable and uninstall require a
  restart.** The UI says so and offers "Restart now".
- Reason: Avalonia caches templates, styles, compiled bindings and visual types in static
  registries; ReactiveUI/Splat roots objects in `Locator`; any one of those pins the ALC.
  Cooperative unload in-process is a research project, not a feature.
- Plugin implementers must still assume their ALC may be collected at shutdown: no
  static events pointing at host objects, no `Locator.Current` mutation.

## 8. Shell integration

### 8.1 Shell shape

The shell is the only component that renders contributions. Planned skeleton:

```
MenuBar
┌───────────┬─────────────────────────────────────────────┬───────────┐
│ LeftTool  │  Document region (tabs)                     │ RightTool │
├───────────┴─────────────────────────────────────────────┴───────────┤
│ BottomTool (collapsible)                                             │
├──────────────────────────────────────────────────────────────────────┤
│ StatusBar                                                            │
└──────────────────────────────────────────────────────────────────────┘
```

This maps 1:1 to Eremex `DocumentPane`/`ToolWindow` later; the plugin API never mentions
Eremex. The first shell version can be a `Grid` + `TabControl` with no docking.

### 8.2 Sink contract

The shell implements one interface, so it can be swapped for a docking shell:

```csharp
public interface IShellContributionSink
{
    void AddDocument(DocumentContribution document);        // non-generic facade
    void AddToolPanel(ToolPanelContribution panel);
    void AddMenuItem(MenuContribution item);
    void AddSettingsSection(SettingsContribution section);
    void RemovePluginContributions(string pluginId);
}
```

`IContributionRegistry` remains the source of truth; the sink is a projection. If the
shell is not ready (e.g. during headless tests), contributions accumulate and can be
asserted without any UI.

### 8.3 Data-driven collections

The shell subscribes with DynamicData, not with imperative `Add` calls from plugins:

```csharp
_registry.Items.Connect()
    .Filter(x => x.Descriptor is IDocumentContribution)
    .Transform(x => x.Descriptor)
    .ObserveOn(RxApp.MainThreadScheduler)
    .Bind(out _documents)
    .Subscribe();
```

Consequences: plugin loading is incremental and observable; removal is free; the shell
has no plugin-specific code paths.

### 8.4 Layout persistence

Layout keys are namespaced by plugin id and contribution id
(`layout/<pluginId>/<contributionId>`). When a plugin disappears, its layout entries are
kept for a grace period (so a reinstall restores position) and pruned after N starts.

## 9. Install / update / uninstall (v1)

- Sources: `<app>/plugins/` (bundled, read-only) and
  `%LOCALAPPDATA%\Mirai.Space\plugins/<id>/` (user). Same id in both → user wins, warning
  logged.
- Install: a `.miraiplugin` file (zip) or a folder. The plugin manager unzips to
  `<root>/staging/<guid>/`, validates the manifest, then moves to `<root>/<id>/` and
  marks the state as `restart required`.
- Update: same flow; the old directory is moved to `trash/` and deleted on next start
  (file locks).
- Uninstall: mark removed in `plugins.state.json`; delete on next start; if the folder is
  unlocked, delete immediately.
- `plugins.state.json` (host-owned): `{ "schemaVersion": 1, "plugins": { "<id>": {
  "enabled": true, "version": "1.2.0", "source": "user", "pendingRestart": "install" } } }`.
  Unknown ids are preserved — a plugin installed by another user profile copy is not lost.
- Disable: state flag only; the folder stays on disk, so re-enabling is instant after
  restart.

## 10. Security and trust

In-process .NET code is full trust. Microsoft's guidance is explicit: untrusted code
cannot be safely loaded into a trusted process. Therefore:

- Installing and enabling a plugin is equivalent to running an executable. This is stated
  in the plugin manager, not buried in docs.
- The plugin manager shows the source folder, the manifest author and a SHA-256 of the
  entry assembly, and never auto-installs anything.
- v1 accepts plugins from local disk only (bundled or user-installed). No download from a
  URL, no auto-update.
- Signing (NuGet-style or Ed25519 over the package) is the prerequisite for any
  marketplace and is a later phase; the manifest already reserves the fields for it.
- Plugins get no capability API: they can use `HttpClient`, files, etc. directly. A
  permission model only makes sense once plugins are out-of-process.
- Contributed views are rendered by the host in-process; there is no HTML/script surface.

## 11. Browser and static modules

- `Mirai.Space.Browser` references `Plugins.Abstractions` only. No ALC, no file I/O.
- Static modules are registered with `services.AddPluginModule<TModule>()` (Tier A). They
  are resolved from host DI and initialized by the same `IPluginHost`, so the shell code
  path is identical.
- The SDK's static host implementation must avoid `System.Reflection.Emit`,
  `Assembly.LoadFrom` and file APIs so it stays WASM-safe. The loader project is simply
  not referenced by Browser, which makes this a compile-time guarantee.

## 12. Diagnostics: plugin manager

A page in the shell (and, before that, a service) exposing:

| Column | Source |
| --- | --- |
| id, name, version, apiVersion | manifest |
| state (`Ready`/`Failed`/`Incompatible`/`Disabled`) | descriptor |
| source, location | descriptor |
| load time, contribution counts | descriptor + registry |
| error (first line + stack in details) | descriptor |

Actions: enable/disable, uninstall, open folder, open log, copy diagnostics report
(`plugin.json` + state + host version + OS). A restart banner appears when any plugin is
`pendingRestart`.

`IPluginHost` exposes `IReadOnlyList<PluginDescriptor> Plugins` and
`event EventHandler<PluginStateChangedEventArgs> StateChanged` so the page is data-driven.

## 13. Versioning and compatibility

- `PluginApi.Current = new Version(1, 0)`; `IPlugin` and contribution descriptors are the
  public contract.
- Contract assembly `AssemblyVersion` is pinned at `1.0.0.0` and **never** changes within
  a major. Plugins binding to `1.0.0.0` keep working across host minor/patch releases.
- Within a major: additive only. New optional interface members via new interfaces
  (`ISomething2`) or default interface methods; `[Obsolete]` for at least one minor
  release before removal.
- `apiVersion.major` mismatch → `Incompatible` before load. `minHostVersion` is checked
  against the host's informational version.
- No strong naming on contract or host assemblies; strong naming would turn every
  assembly into a binding-identity problem and add nothing here.
- Plugin template imports the host's central package versions, so "compiled against the
  host stack" is the default, not a discipline.

## 14. Testing strategy

| Test | What it proves |
| --- | --- |
| Manifest parsing/validation (invalid JSON, bad id, path traversal, bad SemVer, apiVersion mismatch) | Nothing loads on bad input |
| ALC isolation: two plugin folders with different versions of the same dependency | Each gets its own copy; host types are shared |
| Shared-stack enforcement: plugin assembly referencing Splat resolves to host copy | No duplicate Avalonia/ReactiveUI |
| Failure isolation: broken plugin → other plugins load, registry has no items from the broken one | Startup resilience |
| Timeout: plugin that never returns from `InitializeAsync` | App starts, plugin reported `Failed` |
| Unload: load → shutdown → GC → `WeakReference<AssemblyLoadContext>.IsAlive == false` | No static leaks in the loader |
| Headless UI: contributed document renders in a window (needs `Avalonia.Headless` — package approval) | View factory + `DataContext` rule |
| Architecture: `Plugins.Abstractions` references none of the host projects | Contract hygiene |
| End-to-end: `samples/` plugin built, copied to temp `plugins/`, host loads it | The real path works |

Test fixtures need the sample plugin's build output. Approach: sample projects build in the
solution; tests locate `samples/*/bin/<Configuration>/net10.0/` and copy it into a temp
folder. CI adds one `dotnet publish` per sample to verify packaging.

New packages needed (require approval per `AGENTS.md`): `Avalonia.Headless`,
`Avalonia.Headless.XUnit` (or reuse `Avalonia.Headless` only), and optionally
`NetArchTest.Rules`.

## 15. Packaging, template, dev loop

- Plugin project shape:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <EnableDynamicLoading>true</EnableDynamicLoading>
  <Nullable>enable</Nullable>
  <IsPackable>false</IsPackable>
</PropertyGroup>
<ItemGroup>
  <ProjectReference Include="..\..\Mirai.Space.Plugins.Abstractions\Mirai.Space.Plugins.Abstractions.csproj">
    <Private>false</Private>
    <ExcludeAssets>runtime</ExcludeAssets>
  </ProjectReference>
</ItemGroup>
```

- `dotnet new mirai-plugin` template (name, id, icon) that produces the manifest, the
  `IPlugin` implementation and the csproj above.
- Dev loop: `MIRAI_PLUGIN_PATHS` environment variable adds directories to discovery
  (overrides for the same id), so a plugin's `bin/Debug/net10.0` is loaded directly.
  Restart to reload.
- Build script packs `plugin.json` + published output into `<id>-<version>.miraiplugin`.

## 16. Roadmap

| Phase | Deliverable | Acceptance |
| --- | --- | --- |
| P0 | This document + decisions in §19 | Approved in review |
| P1 | `Plugins.Abstractions`, `Plugins` loader, static host, sample + broken plugins, tests | Sample plugin loads, contributes, fails safely; unload test green |
| P2 | Shell skeleton: menu, documents (tabs), one tool panel, settings page; contributions rendered | Sample plugin's document opens and renders; host has no plugin references |
| P3 | Plugin manager page, state file, install/uninstall zip, restart-required flow | Install/uninstall/disable round-trip with restart |
| P4 | Eremex docking adapter behind `IShellContributionSink`, layout persistence | Same plugin renders in docked layout without changes |
| P5+ | Signing, marketplace, out-of-process/WASM host, hot reload research | Not scoped |

P1 and P2 should land in the same release cycle: an SDK without a shell cannot be
validated, and a shell written before the SDK risks a wrong region model.

## 17. Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Shell does not exist yet; SDK designed against an imagined shell | API churn after P2 | Thin sink interface; P1 and P2 in one cycle; sample plugin is the acceptance test |
| Avalonia static caches make ALC unload unreliable | "Reload" never ships | Restart-only product story; unload only asserted in tests |
| Plugins compiled against a different Avalonia/ReactiveUI version | Runtime `MissingMethodException` | Template pins host CPM versions; loader logs the requested vs host version in the failure message |
| Third-party NuGet dependency version conflicts inside one ALC | Plugin fails to load | Each plugin has its own folder and ALC; error message names the missing assembly |
| Trimming/AOT publish breaks reflection-based loading | Desktop publish configuration blocks plugins | Keep `PublishTrimmed=false` for the desktop plugin-enabled build; document the constraint |
| `Locator.Current` / static event leaks from plugins | Memory growth, pinned ALC | Contract rules + review; unload test in CI |
| Eremex licensing/availability blocks docking | Shell rework | Regions abstract docking from day one |
| Plugin authors treat a 1.0 API as frozen forever | Slow API evolution | Publish apiVersion policy before any third-party plugin exists |

## 18. Open questions (need a decision)

1. **Audience.** Internal teams only, or external authors/marketplace? Internal-only would
   let us skip signing and keep the contract loosely versioned; external authors change
   the security and compatibility bar.
2. **Browser parity.** Must every first-party feature work in Browser? If yes, features go
   into Tier A and the directory loader stays a desktop-only extra.
3. **Hot reload in development.** Is "restart the app" acceptable for the daily plugin
   loop, or is a reload-without-restart a hard requirement? The latter changes the shell
   teardown design.
4. **Contribution set.** Confirm the v1 list (documents, tool panels, menu items,
   settings, status bar) and which one is missing for the first real feature.
5. **Plugin dependencies.** May plugins bring arbitrary NuGet dependencies in v1, or is the
   allowed dependency set "SDK + BCL" until the loader has more mileage?
6. **Server coupling.** Will a UI plugin ever need a matching server-side module? If yes,
   the package format and manifest need a second entry point now, not later.
7. **Distribution.** Folder/zip only, or a feed/installer with versions and updates?
8. **Docking timing.** Build the shell directly on Eremex Docking, or start with plain
   tabs and add docking in P4? (Recommendation: plain first, adapter in P4.)
9. **API stability promise.** From which release is the contract frozen for third parties?

## 19. Proposed `DECISIONS.md` entries

Append after approval, one line each:

- 2026-11-21 — UI plugins use a manifest-first, contribution-point model; the shell never
  references plugin types or the loader project.
- 2026-11-21 — Two tiers (static modules + directory plugins) share one contract so
  Browser keeps working without a loader.
- 2026-11-21 — Third-party plugins run in a collectible ALC with a child service provider;
  the host never resolves plugin services.
- 2026-11-21 — Plugin services never register into the host container; isolation and
  unloadability beat convenience.
- 2026-11-21 — Install/update/disable/uninstall require a restart in v1; no hot reload.
- 2026-11-21 — In-process plugins are fully trusted; no sandbox and no auto-install in v1.
