# Avalonia 12 client: generic host, DI composition and ReactiveUI 27

Verified on 2026-11-21 against the packages actually restored in this repository.
Written after wiring `Mirai.Space.Desktop` to `CP.Extensions.Hosting.Avalonia` and moving the
view models to `Mirai.Space.Presentation`.

## Verified versions

| Package | Version | Note |
| --- | --- | --- |
| `CP.Extensions.Hosting.Avalonia` | 6.1.1 | brings `Avalonia` 12.1.3, `Microsoft.Extensions.*` 10.0.12 |
| `ReactiveUI` | 27.0.1 | brings `ReactiveUI.SourceGenerators` 5.0.0, `Splat` 22.0.1 |
| `ReactiveUI.Avalonia` | 12.1.6 | pairs with ReactiveUI 26/27 API, `Avalonia` 12.1.3 |
| `DynamicData` | 9.4.33 | brings `System.Reactive` 6.1.0 |
| `Microsoft.Extensions.Hosting` | 11.0.0 | host builder |
| `Avalonia` | 12.1.6 | UI stack |

`ReactiveUI.Avalonia` 12.0.3 must **not** be paired with `ReactiveUI` 27: it pins `ReactiveUI` 23.2.28 and
`Splat` 19.4.1, while `ReactiveUI` 27 needs `Splat` 22.0.1. 12.1.6 is the compatible line.

## Desktop host shape

`Mirai.Space.Desktop/Program.cs`:

```csharp
[STAThread]
public static void Main(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddDesktop();
    builder.ConfigureAvalonia(avalonia => avalonia
            .UseApplication(typeof(App))
            .ConfigureAppBuilder(appBuilder => appBuilder
                .WithInterFont()
                .UseReactiveUI(_ => { })))
        .UseAvaloniaLifetime();

    using var host = builder.Build();
    host.Run();
}
```

Rules that are easy to get wrong:

- **`[STAThread]` is mandatory.** On Windows `AvaloniaHostedService.StartAsync` throws `InvalidOperationException`
  when the entry thread is not STA, and the host runs the Avalonia lifetime on that same thread.
- **`UseApplication(typeof(App))` is mandatory.** Without it `AvaloniaApplicationResolver` returns `null` and throws
  `InvalidOperationException("Unable to initialize the Avalonia application.")`. It registers `App` as a singleton,
  so `App` may take constructor dependencies.
- `UseAvaloniaLifetime()` must be called after `ConfigureAvalonia(...)`, otherwise it throws
  `NotSupportedException("Please configure Avalonia first!")`.
- **No `IAvaloniaShell` is registered, and that is deliberate.** The hosted thread shows only
  `GetServices<IAvaloniaShell>()`; with an empty list the loop in `AvaloniaThread.UiThreadStart` simply does nothing and
  leaves `lifetime.MainWindow` alone, so `App` is free to create the window itself.
  Source: <https://github.com/reactivemarbles/Extensions.Hosting/blob/main/src/Extensions.Hosting.Avalonia/Internals/AvaloniaThread.cs>
  Registering a shell type is the alternative, but it hands window creation to the hosting package and leaves no seam
  for showing something else first (a loader), so this repository does not use it.
- `App` creates the window, because it is the only place that knows the platform lifetime:

```csharp
public partial class App : AvaloniaApplication
{
    private readonly Lazy<MainWindowViewModel> _mainWindowViewModel;
    private readonly Lazy<MainViewModel> _mainViewModel;

    public App(Lazy<MainWindowViewModel> mainWindowViewModel, Lazy<MainViewModel> mainViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
        _mainViewModel = mainViewModel;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow
            {
                DataContext = _mainWindowViewModel.Value
            };

            desktop.MainWindow = window;
            window.Show();
        }

        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = _mainViewModel.Value
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

- `App` is a constructor-injected singleton (`UseApplication(typeof(App))` registers it as such), so the window never
  reaches into a container and never receives a view model through its own constructor. That is also why the browser
  host builds `App` through a container instead of `AppBuilder.Configure<App>()` — that overload demands a public
  parameterless constructor.

### Why there is no `CP.Extensions.Hosting.ReactiveUI.Avalonia`

That bridge (6.1.1) depends on `ReactiveUI` 26.0.1 and `Splat.Microsoft.Extensions.DependencyInjection` **21.0.0**,
while `ReactiveUI` 27 requires `Splat` 22.0.1 — and the Splat DI adapter exists only at 21.0.0. Mixing them risks
runtime failures, so the app uses `.UseReactiveUI(_ => { })` inside `ConfigureAppBuilder` instead of the Splat-aware
host integration.

## DI composition

- Every client project exposes a **flat** `IServiceCollection Add<Layer>()` extension in
  `Extensions/ServiceCollectionExtensions.cs`. It registers only its own services and returns the collection.
- No `Add<Layer>()` calls another layer's `Add<Layer>()`. The composition root (the host project) is the only place
  that knows the full chain, and it composes the layers it actually uses:
  `services.AddMvvmExtensions().AddPresentation()`. Empty `Add<Layer>()` shells are not called.
- Rationale: hidden transitive registration makes the resulting graph depend on which extension happens to be called
  and hides the real dependency order. Recorded in `Z:\Work\DECISIONS.md`.
- Extensions use the C# 14 **extension block** form, not `this`-parameter static methods:
  `extension(IServiceCollection services) { public IServiceCollection AddCore() { ... } }`. Both forms emit the same
  IL and are source/binary compatible, but the block keeps the receiver in scope for every member and leaves room for
  extension properties later. The repo builds with the .NET 10 SDK, so the feature is available; use the newest
  language feature the SDK supports unless it makes the code harder to read.
  Source: <https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/extension>
- View models are registered in the presentation layer (`AddPresentation`: `AddSingleton<MainViewModel>()`,
  `AddSingleton<MainWindowViewModel>()`). `App` takes **both** view models as `Lazy<T>`: the desktop branch reads
  `Lazy<MainWindowViewModel>`, the single-view branch reads `Lazy<MainViewModel>`, so neither platform constructs the
  other's view model — a WebAssembly build never creates `MainWindowViewModel`.
  `Lazy<T>` is only ever useful for a view model here, so the adapter lives in `Mirai.Space.Extensions.MVVM`
  (`LazyResolved.cs` at the project root): it resolves `T` from the provider on the first `Value` read, while the
  container still owns the single instance. `AddMvvmExtensions()` in that project's
  `Extensions/ServiceCollectionExtensions.cs` calls `AddLazy()`, which registers it as the open generic `Lazy<>`; the desktop host
  calls `.AddMvvmExtensions().AddPresentation()`, the browser composition root does the same and then builds
  its provider. `AddPresentation` stays a plain view-model registration, because a layer registers only what it owns.
- `IServiceProvider` injection is **not** used here — it is a service locator and hides the dependency.
- Where an extension lives and how the folder is named: `Extensions/` holds **extension classes only** — per project
  that is `Extensions/ServiceCollectionExtensions.cs`, never a file beside the services it registers, and never a plain
  class that happens to be used by them. Adapting/implementing types stay outside that folder
  (`LazilyResolved.cs` sits at the project root), because a folder named after a language feature must not become a
  junk drawer. A project name must say what the project contains; a bare `Mirai.Space.Extensions` is untraceable
  ("what does it do?") and was rejected for that reason, while `Mirai.Space.Extensions.<Topic>` (`MVVM`, `Reactive`,
  `Http`, `SignalR`, `Avalonia`) states the topic.
- `Mirai.Space.UI` itself registers nothing, because views are created by the application object, not by DI.

## ReactiveUI 27 views

`ReactiveUI.Avalonia` 12.1.6 provides `ReactiveWindow<TViewModel>` and `ReactiveUserControl<TViewModel>` in the
`ReactiveUI.Avalonia` namespace (both sit on a non-generic base owning the `ViewModel` Avalonia property and
implementing `IViewFor<T>` explicitly).

```csharp
public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}

public partial class MainView : ReactiveUserControl<MainViewModel>
{
    public MainView()
    {
        InitializeComponent();
    }
}
```

The `.axaml` root names the **concrete view class itself** — `views:MainWindow` / `views:MainView`, not a framework base
and not the ReactiveUI base either. Avalonia's XAML compiler populates the instance the class constructor already
created, so a root type equal to the `x:Class` type is legal (unlike WPF, where the root must be a base class), and the
visual designer/populator resolves properties against the real class.

```xml
<views:MainWindow xmlns="https://github.com/avaloniaui"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:views="using:Mirai.Space.UI.Views"
                  xmlns:vm="using:Mirai.Space.Presentation.ViewModels"
                  x:Class="Mirai.Space.UI.Views.MainWindow"
                  x:DataType="vm:MainWindowViewModel"
                  Title="Mirai.Space">
    <views:MainView DataContext="{Binding Main}" />
</views:MainWindow>
```

`x:DataType` stays: it types the compiled bindings inside the file. No `x:TypeArguments` (the concrete class is closed)
and no `Design.DataContext` — a view never carries a design-time instance of its view model.

### One slot, not two: `DataContext` vs `ViewModel`

`ReactiveWindowBase`/`ReactiveUserControlBase` keep the typed `ViewModel` property and `DataContext` **in step in both
directions** — `ViewModelPropertySync.Apply` copies `DataContext` into `ViewModel` when the old `ViewModel` equals the old
`DataContext`, and copies `ViewModel` into `DataContext` when the old `DataContext` equals the old `ViewModel`
(`ReferenceEquals` guards stop the loop).
Source: <https://github.com/reactiveui/ReactiveUI.Avalonia/blob/main/src/ReactiveUI.Avalonia/ViewModelPropertySync.cs>

Consequences, which are the house rule here:

- Assigning **one** of the two is enough. Assigning both is redundant noise and reads like two independent state owners.
- Bindings read `DataContext`, so `DataContext` is the property the application assigns. `ViewModel` is then populated
  automatically, and `this.WhenActivated(...)` activation forwarding keeps working.
- **Never assign `DataContext` in a view constructor.** The window/view stays view-model agnostic and keeps a
  parameterless constructor, which also keeps the runtime XAML loader and the previewer happy (no `AVLN3001`) and makes
  the view constructible without DI. The owner of the pairing is `Mirai.Space.UI.App`, which creates both the desktop
  window and the single-view root.
- `ReactiveWindow<T>` / `ReactiveUserControl<T>` never create a view model. A view with a typed base and no
  `DataContext` renders an empty surface and logs nothing — the classic time sink.

## ReactiveUI.SourceGenerators 5.0.0

The generator ships transitively with `ReactiveUI` 27 (no explicit `PackageReference` is needed for a project that
already references `ReactiveUI`). It replaces `RaiseAndSetIfChanged` boilerplate:

```csharp
using ReactiveUI.SourceGenerators;

public sealed partial class MainViewModel : ViewModelBase
{
    [Reactive]
    public partial string Greeting { get; set; } = "Welcome to Mirai.Space!";
}
```

- Partial-property `[Reactive]` needs C# 13 and a Roslyn band that supports it: `roslyn4.14` (VS 17.14 / .NET 9 SDK)
  or `roslyn5.0` (.NET 10 SDK). This repo builds with the .NET 10 SDK, so the `roslyn5.0` band loads.
- The property must be `partial`, the class must be `partial`, and both accessors are left empty. `virtual`,
  `override`, `required`, `private`/`protected` setters and (C# 14) the initializer are written on the property.
- Generated code is emitted in memory; enable `EmitCompilerGeneratedFiles` only for debugging. A `partial` property
  without a generated implementation fails with `CS9248`, so a green build proves the generator ran.
- For classes that cannot derive from `ReactiveObject` use `[IReactiveObject]`; commands come from
  `[ReactiveCommand]`.

## Code style in this repository

Order of members inside a type: fields, **properties**, constructors, methods. StyleCop's `SA1201` default puts
constructors before properties; this repo puts properties first on purpose — the public surface of a view model is the
first thing a reader needs, the constructor plumbing is not.

Comments stay quiet:

- No XML docs on self-evident members. `services.AddSingleton<MainViewModel>();` does not need a `<summary>`.
- A comment explains **why**, never **what**. One or two lines, at the line that needs them: a workaround, a platform
  quirk, a link to the API that forced the shape.
- No commented-out code, no `TODO` without an issue or a name, no banners, no `#region`.
- If a member needs a paragraph of explanation, the design is wrong: fix the shape first, keep at most one line.

## Gotchas hit in this repository

| Symptom | Cause | Fix |
| --- | --- | --- |
| `CS0118: "Application" is a namespace but is used like a type` in `Mirai.Space.UI` | the project now references `Mirai.Space.Application`, whose namespace shadows `Avalonia.Application` inside `namespace Mirai.Space.UI` | `using AvaloniaApplication = Avalonia.Application;` and derive from the alias |
| `AVLN3001: XAML resource won't be reachable via runtime loader, no public constructor` | appears only while the view has a DI-only constructor | keep a parameterless constructor and let `App` assign `DataContext` (the `NoWarn` that used to be in `Mirai.Space.UI.csproj` is gone) |
| `NU1010` on `MessagePack` in `Mirai.Space.Request` | `PackageReference` without a `PackageVersion` in the CPM file | pin `MessagePack` 3.1.12 in `Directory.Packages.props` |
| 19 × `NU1507` (two package sources: nuget.org + Avalonia) | CPM without package source mapping | not fixed yet: add `<packageSourceMapping>` to `nuget.config` |
| Two owners of the view model (`DataContext` **and** `ViewModel` assigned, or `DataContext` inside a view ctor) | `ViewModelPropertySync` mirrors the two properties, so a second assignment is dead weight and hides who owns the pairing | assign only `DataContext`, only in `App` |
| `CS0310: 'App' must be a non-abstract type with a public parameterless constructor` in `Mirai.Space.Browser` | `AppBuilder.Configure<TApp>()` instantiates the type itself, so it cannot build an `App` that takes constructor dependencies | `AppBuilder.Configure(() => ActivatorUtilities.CreateInstance<App>(provider))` with the browser host's own `ServiceCollection` |
| `InvalidOperationException: Unable to resolve service for type 'System.Lazy`1[...]'` | `Microsoft.Extensions.DependencyInjection` has no built-in `Lazy<T>` support | call `AddLazy()` from `Mirai.Space.Extensions.MVVM.Extensions` in the host's composition root (it registers `LazyResolved<T>` as the open-generic `Lazy<>`) |


## Adding a new client project

1. Create the project, add `Extensions/ServiceCollectionExtensions.cs` with a flat `Add<Layer>()`.
2. Add `Microsoft.Extensions.DependencyInjection.Abstractions` (or a heavier `Microsoft.Extensions.*` package if needed).
3. Add the project reference **from the host** and one line to the host's fluent chain — never call the new
   extension from another layer's `Add<Layer>()`.
