# Extensible table component — concept specification

Status: draft for review
Owner: client platform
Last updated: 2026-11-21

Stack pinned by `Directory.Packages.props`: Avalonia 12.1.6, ReactiveUI 27.0.1,
ReactiveUI.Avalonia 12.1.6, DynamicData 9.4.33, Microsoft.Extensions.* 11.0.0.
The grid vendor (Eremex) is **not referenced yet** — see §2.4.

This document is a design contract for a reusable table (DataGrid) component, not an
implementation plan of record. Code blocks are signatures to agree on, not final code.

---

## 1. Purpose, scope, assumptions

### 1.1 What we are building

One table component that covers the majority of client screens, where the table is
never the feature — the *data and the actions on it* are. Screens differ in only four
ways:

1. **where rows come from** (HTTP GET snapshot, SSE stream, SignalR deltas, polling);
2. **what the columns look like** (auto-generated from a DTO, overridden per screen);
3. **which actions exist** (row commands, column-menu commands, toolbar commands) and
   who may see them (roles, permissions, selection state);
4. **what plugins add** (layout persistence, export, selection memory, audit).

Everything else — selection, menus, templates, threading, activation, empty/error
states — must be identical across screens. That is the whole point of the component:
inconsistency between tables is a bug class we want to delete.

### 1.2 v1 scope

| In scope | Out of scope (v1) |
| --- | --- |
| Snapshot + live-delta data sources | Server-side paging / infinite scroll (§13, M4) |
| Auto-generated columns + VM overrides | In-place cell editing (§8.4) |
| Row / column-menu / toolbar commands with policy filtering | Drag-and-drop row reordering |
| DataTemplate resolution from DI | Excel-like aggregation rows |
| Table plugins (behavior + view) | Chart / pivot views |
| Layout save/restore, export | Master-detail inside one grid (§13, open question) |
| Empty, loading, error, stale states | Multi-table orchestration in one screen |

### 1.3 Assumptions to confirm

- **A1**: rows are immutable DTOs from the Application/Contracts layer, not per-row
  view models (justified in §4.6 and §8).
- **A2**: every row has a stable key (`Guid`, `int`, or `string`) available on the DTO.
- **A3**: the Eremex grid remains the only grid; no second vendor adapter in v1, but
  the Presentation-layer seams must not assume it (§3).
- **A4**: a table instance is bound to one screen lifetime, not global.

---

## 2. Verified platform facts this design depends on

Fetched from vendor docs on 2026-11-21 (links inline). These are load-bearing — several
decisions below are direct consequences.

### 2.1 The hybrid column model is a vendor feature, not a hack

`DataGridControl` exposes both `Columns` (explicit `GridColumn` collection) and
`ColumnsSource` — a collection of *business objects* that generate columns through
`ColumnTemplate` of type `ITemplate<object, GridColumn>`. The documented pattern is
exactly what the request needs:

```xml
<mxdg:DataGridControl ColumnsSource="{Binding Columns}"
                      ColumnTemplate="{Binding ColumnTemplate}"
                      AutoGenerateColumns="True" />
```

Missing columns are auto-generated, so VM-declared columns and reflection-generated
columns coexist. Re-assigning `ItemsSource` deletes auto-generated columns and
regenerates them — this constrains the wrapper (§13, T2).
Source: <https://eremexcontrols.net/controls/datagrid/columns>

### 2.2 Menus are extensible at two fixed points

- `DataGridControl.ColumnMenu` is a `PopupMenu` with built-in sort/group/search/
  column-chooser items; its DataContext is the `GridColumn`, the column's own
  DataContext is the grid VM. It can be customized after `Initialized` or replaced.
- `DataGridControl.RowCellMenu` starts empty; its DataContext is a `CellData` with
  `.DataControl` and `.Row`. Items are provided through `ItemsSource` + `PopupMenu.Styles`
  binding `Header`, `Command`, `CommandParameter`.
- `PopupMenu.Opening` allows rebuilding items per open.

Source: <https://eremexcontrols.net/controls/datagrid/context-menus>

### 2.3 Selection, counts and batching exist natively

`SelectionMode` is `RowSelectionMode { Single, Multiple }`; `SelectedItems` is a
mutable collection of business objects; `FocusedItem`, `FocusedColumn`, `FocusedRowIndex`;
`SelectionChanged` event; `SelectRow` / `SelectRange` / `SelectAll` / `ClearSelection`.
`VisibleRowCount` gives the post-filter row count. `BeginDataUpdate()` / `EndDataUpdate()`
batch refreshes and `RefreshData()` re-applies sort/filter.
Source: <https://eremexcontrols.net/API/Eremex.AvaloniaUI.Controls.DataControl/DataControlBase/>

`SaveLayout(Stream)` / `RestoreLayout(Stream)`, `ExportToXlsx|Pdf|Images|Csv` and
`CopyToClipboardAsync` live on the same base type, so layout persistence and export are
plugins over existing API rather than new code.

### 2.4 Two hard prerequisites

1. Eremex packages come from the vendor feed with credentials, are commercial, and the
   whole `Eremex.*` family must share one version. The theme dictionary must be in
   `Application.Styles` before the first control is created.
2. The grid is **not** a server-paged control. It materializes `ItemsSource` and
   filters/sorts/groups locally. Any "10k+ rows" story is a paging story (§13, T1).

---

## 3. Decomposition

Five seams, one per question the component answers. Dependency direction is strictly
downward; `Mirai.Space.Presentation` must not reference Avalonia or Eremex.

```
             ┌──────────────────────────────────────────────┐
  UI         │ TableGrid (UserControl, ITableViewPort)      │
             │ TableColumnTemplate, template/icon catalogs  │
             └───────────────▲──────────────────────────────┘
                             │ implements port, binds non-generic VM surface
             ┌───────────────┴──────────────────────────────┐
 Presentation│ ITableViewModel / TableViewModel<T>          │
             │ TableDefinition<T>, TableContext<T>          │
             │ command aggregator + policies, plugins       │
             └───────────────▲──────────────────────────────┘
                             │
             ┌───────────────┴──────────────────────────────┐
 Application │ ITableSource<T>, loaders, feeds              │  (owned by feature code,
 Data        │ ITableMutator<T>                             │   not by the component)
             └──────────────────────────────────────────────┘
```

Naming rule: `Table*` types are **definitions** (immutable, DI, shared);
`TableViewModel<T>` is **runtime state** (per screen). Anything holding a `CancellationToken`,
a subscription, or a grid reference is runtime state.

---

## 4. Data layer — `ITableSource<T>`

### 4.1 The contract

```csharp
namespace Mirai.Space.Presentation.Tables.Data;

/// <summary>Transport-agnostic row feed. The only thing the table knows about data.</summary>
public interface ITableSource<T>
{
    /// <summary>Cold. Each subscription starts the source; the last disposal stops it.</summary>
    IObservable<IChangeSet<T>> Connect();

    /// <summary>Loading/refreshing/live/error state, current value on subscribe.</summary>
    IObservable<TableSourceState> State { get; }

    /// <summary>Ask for a full re-sync. No-op for pure snapshots that already reloaded.</summary>
    Task RefreshAsync(CancellationToken cancellationToken);
}

public sealed record TableSourceState(
    bool IsLoading,
    bool IsRefreshing,
    bool IsLive,
    TableSourceError? LastError,
    DateTimeOffset? LastUpdatedAt);

public sealed record TableSourceError(TableSourceErrorKind Kind, string Message, Exception? Exception);

public enum TableSourceErrorKind { Network, Unauthorized, Forbidden, Server, Protocol }
```

`Connect()` is the requested seam: whether the bytes arrive over HTTP, SSE, or SignalR is
invisible above this interface. It is cold and `RefCount`-ed, so connecting is tied to
`TableViewModel<T>.Activate()` and disconnecting to disposal (§10.4) — no leaked
subscriptions when a screen closes.

**Decision — no `Reset()` on the source.** A `Reset` implies the source can be
re-targeted (different customer, different filter) while the grid, selection, sort and
column state survive. That is untrue in practice: the columns, commands, permissions and
default sort of a new query are a different *definition*. Re-targeting is therefore a new
`TableOpenContext` → new source instance → new view model (§10.1). What the source does
internally on reconnect or on `RefreshAsync` is its own business, which is exactly the
"source rules its own updates" property that was wanted.

### 4.2 Snapshot and deltas are separate seams

HTTP GET and SignalR are not alternatives, they are two halves of the same table: nobody
wants a grid that is empty until the next delta arrives, and nobody wants a grid that
only updates on F5. So the component ships the composition, and each transport
implements one small interface:

```csharp
/// <summary>Fetches the current full row set.</summary>
public interface ITableSnapshotLoader<T>
{
    Task<IReadOnlyList<T>> LoadAsync(TableOpenContext context, CancellationToken cancellationToken);
}

/// <summary>Subscribes to incremental changes; the first emission is the replay/resync.</summary>
public interface ITableChangeFeed<T, TKey> where TKey : notnull
{
    IObservable<IChangeSet<T, TKey>> Connect(TableOpenContext context);
}
```

Shipped implementations:

| Type | Composed of | Typical transport |
| --- | --- | --- |
| `SnapshotTableSource<T>` | `ITableSnapshotLoader<T>` | HTTP GET |
| `LiveTableSource<T, TKey>` | `ITableChangeFeed<T, TKey>` | SignalR, SSE |
| `SnapshotAndFeedTableSource<T, TKey>` (default) | both | GET snapshot, then subscribe |
| `PollingTableSource<T>` | `ITableSnapshotLoader<T>` + interval | any REST endpoint |

`SnapshotAndFeedTableSource` defines merge semantics once, for everyone:

1. subscribe to the feed **first**, buffer emissions;
2. load the snapshot, apply it as `Edit`-clear + `AddOrUpdate` range;
3. drain the buffer, applying deltas whose sequence/version is newer than the snapshot's;
4. on feed reconnect, call `RefreshAsync` (new snapshot) instead of trusting a gap-filled
   stream.

Step 3 requires an ordering token. For SignalR that is the server-assigned version or
sequence number; for SSE the event id. If the server has neither, the feed must emit
whole-row replacements and step 3 is a plain `AddOrUpdate` — acceptable, documented as a
degraded mode.

### 4.3 What a source factory is for

```csharp
public interface ITableSourceFactory<T>
{
    ITableSource<T> Create(TableOpenContext context);
}

public sealed record TableOpenContext(
    string TableId,
    IReadOnlyDictionary<string, object?> Arguments);
```

Arguments are the screen's context (`customerId`, `projectId`, …). They are weakly typed
on purpose — this is the one place where a generic table cannot be typed, and a small
dictionary beats a fishing expedition of generic arguments. Keys are constants declared
next to the definition.

### 4.4 Row identity

Selection survives a live update only if the updated row is recognized as the same row.
The UI layer restores selection by key (§8.2), so the definition needs a key selector
that is independent of the transport:

```csharp
public sealed class TableDefinition<T>
{
    /// <summary>Stable identity for selection restore and diffing. Null = reference identity.</summary>
    public Func<T, object>? RowKey { get; init; }
}
```

For `LiveTableSource<T, TKey>` the same key must be used as the DynamicData key. Duplicate
keys throw inside `SourceCache`, so the key selector is validated in M0 against real data.

### 4.5 Threading and batching

Sources emit on whatever thread the transport uses. The rule is one hop, at the seam:

```csharp
Source.Connect()
    .ObserveOn(RxApp.MainThreadScheduler)
    .Bind(out var rows)
    .Subscribe()
    .DisposeWith(_subscriptions);
```

`Bind(out ReadOnlyObservableCollection<T>)` is the only place a UI collection appears;
`TableViewModel<T>` owns it, the view only binds. Bursts (a 500-row snapshot) are wrapped
by the wrapper control in `BeginDataUpdate()`/`EndDataUpdate()` — see §10.3.

Testing: `RxApp.MainThreadScheduler` is replaced with `ImmediateScheduler.Instance` in
tests, per the ReactiveUI testing convention already used in this repo's docs.

---

## 5. Shape layer — columns

### 5.1 Column model (Avalonia-free)

```csharp
public sealed class TableColumnModel
{
    public required string Field { get; init; }          // DTO property name, Eremex FieldName
    public string? Header { get; init; }                  // null = from DTO metadata
    public TableColumnWidth Width { get; init; } = TableColumnWidth.Auto;
    public bool IsVisible { get; init; } = true;
    public int SortIndex { get; init; } = -1;
    public TableSortDirection SortDirection { get; init; } = TableSortDirection.None;
    public string? CellTemplateKey { get; init; }         // resolved in UI (§6)
    public string? EditorKey { get; init; }               // resolved in UI (§5.4)
    public bool IsReadOnly { get; init; }
    public bool AllowSorting { get; init; } = true;
    public bool AllowFiltering { get; init; } = true;
    public bool AllowResizing { get; init; } = true;
    public bool ShowInColumnChooser { get; init; } = true;
}

public readonly record struct TableColumnWidth(TableColumnWidthMode Mode, double Value)
{
    public static TableColumnWidth Auto => new(TableColumnWidthMode.Auto, 0);
    public static TableColumnWidth Px(double px) => new(TableColumnWidthMode.Pixel, px);
    public static TableColumnWidth Star(double weight) => new(TableColumnWidthMode.Star, weight);
}

public enum TableColumnWidthMode { Auto, Pixel, Star }
public enum TableSortDirection { None, Ascending, Descending }
```

Why a private width model instead of `GridLength`: `GridLength` is an Avalonia type and
would drag Avalonia into Presentation. The UI layer maps `TableColumnWidthMode` →
`GridLength` in one switch.

### 5.2 Column template

`TableColumnTemplate : ITemplate<object, GridColumn>` is created by DI in the UI layer and
is the single place that turns `TableColumnModel` into `GridColumn`. It sets `FieldName`,
`Header`, width, visibility, sorting/filtering flags, `CellTemplate` (from the template
catalog) and `EditorProperties` (from the editor catalog). Because it is DI-built, it can
depend on both catalogs without the VM knowing anything about XAML.

### 5.3 Generation rules

Precedence, highest first:

1. **VM-declared column** from `TableDefinition<T>.Columns` → `ColumnsSource`;
2. **Auto-generated column** from reflection over `T` when `AutoGenerateColumns = true`;
3. **Suppressed column**: a `TableColumnModel { IsVisible = false }` with no
   `CellTemplateKey` acts as a veto for the auto-generated one (via `AutoGeneratingColumn`
   + `e.Cancel`).

Column order is the interesting part: VM columns carry `SortIndex`/explicit list order,
auto-generated ones carry `Display(Order)`. Mixed ordering is not specified by the vendor
docs and must be verified in M0 (§14, T3). Fallback if interleaving is unstable: after
`AutoGeneratedColumns`, reorder `Columns` by our own computed `VisibleIndex`.

### 5.4 Editors

`EditorProperties` must be assigned per type/field, which is exactly the kind of
boilerplate a catalog removes:

```csharp
public interface ITableEditorCatalog          // UI layer
{
    bool TryCreate(string editorKey, out object editorProperties);   // DataControlColumnBase.EditorProperties
}
```

Registered editors: text, numeric/spin, date, bool, enum combo, reference lookup
(`ComboBoxEditorProperties.ItemsSource` fed by a lookup service). v1 is read-only, so the
catalog is implemented but only exercised in M4.

---

## 6. Templates — `DataTemplate` through DI

### 6.1 Why this is not a trivial problem

A `DataTemplate` is an Avalonia object, and the control that wants it (`TableGrid`) is
created by XAML, so it cannot constructor-inject anything. The usual escape hatches are
bad: a static `App.Services` is a service locator; an `x:Static` resource cannot be
composed per screen. The working shape is: **Presentation decides *which* template by key;
the UI layer *resolves* the key through a DI-built catalog, falling back to the view's own
resource scope.**

### 6.2 Catalog

```csharp
namespace Mirai.Space.UI.Tables;

public interface ITableTemplateCatalog
{
    /// <summary>Resolves a cell template by key: DI registrations first, then resources.</summary>
    bool TryGet(string key, IResourceHost? scope, out IDataTemplate? template);
}
```

- `AddTableTemplate(key, DataTemplate)` registers a template explicitly (used by feature
  modules and by tests);
- unregistered keys are resolved with `scope.TryFindResource(key)` — the `TableGrid`
  passes itself as the scope, so a screen can keep a template in its own
  `<UserControl.Resources>` without touching DI;
- resolution failure is a hard error in development (`Debug.Fail` + visible
  "missing template" placeholder), never a silent default, because a silent default hides
  the bug for months.

### 6.3 Consequences

- `TableColumnModel.CellTemplateKey` is a plain string; Presentation never sees
  `IDataTemplate`.
- Per-type templates (order status badge, currency, avatar cell) live in the UI layer of
  the feature module, not in the table component.
- `TableGrid.axaml` contains **no** per-type cell templates — it cannot, it is
  non-generic. All per-type visuals come from the catalog.

---

## 7. Actions layer — commands

### 7.1 The central decision: commands are table-scoped definitions

A per-row command instance (the classic WPF `RowViewModel.Commands`) forces every row to
be wrapped in a view model. With Eremex that means `FieldName` resolves against the
wrapper, so every property must be duplicated or delegated, and every live update replaces
the wrapper — which destroys selection and edit state. The cost is high and permanent.

Therefore: **one command instance per table**, parameterized by the current context.

```csharp
public interface ITableCommandDescriptor
{
    string Id { get; }                 // stable, for dedup and telemetry
    string Header { get; }
    string? IconKey { get; }           // resolved by ITableIconProvider in UI
    string? Permission { get; }        // policy input, e.g. "orders.delete"
    int Order { get; }                 // within Group
    string? Group { get; }             // ordering bucket
    bool IsDestructive { get; }        // UI asks for confirmation
    ICommand Command { get; }
}

public interface IRowTableCommand<T> : ITableCommandDescriptor
{
    /// <summary>Cheap visibility rule; also used to drop items from menus at build time.</summary>
    bool CanShow(TableContext<T> context) => true;

    /// <summary>Parameter passed to <see cref="ITableCommandDescriptor.Command"/>.</summary>
    object? GetParameter(TableContext<T> context) => context;
}
```

`GetParameter` is the "хитрее" option the request asked about: a command can receive the
whole context (default), the focused row, or the selected rows. `ICommand` stays the
contract because `ReactiveCommand` implements it and both `Button` and
`ToolbarButtonItem` bind to it directly.

```csharp
public sealed class DeleteOrdersCommand : IRowTableCommand<OrderRow>
{
    public string Id => "orders.delete";
    public string Header => "Delete";
    public string? IconKey => "delete";
    public string? Permission => "orders.delete";
    public int Order => 100;
    public string? Group => "danger";
    public bool IsDestructive => true;

    public ICommand Command { get; }

    public DeleteOrdersCommand(IOrdersApi api) =>
        Command = ReactiveCommand.CreateFromTask<TableContext<OrderRow>>(
            (context, cancellationToken) => api.DeleteAsync(
                context.SelectedItems.Select(x => x.Id).ToArray(), cancellationToken));

    public bool CanShow(TableContext<OrderRow> context) => context.HasSelection;
}
```

Note: `CanShow` is intentionally synchronous and must stay cheap (a selection count check,
not a network call). Anything needing I/O is discovered once at table open and refreshed
when the context version changes, never inside a menu-open handler.

### 7.2 Aggregation, and commands that appear at runtime

```csharp
public interface IRowTableCommandProvider<T>
{
    IEnumerable<IRowTableCommand<T>> GetCommands(TableContext<T> context);
}

public interface ITableCommandAggregator<T>
{
    IReadOnlyList<IRowTableCommand<T>> GetRowCommands(TableContext<T> context);
    IReadOnlyList<IColumnMenuCommand<T>> GetColumnCommands(TableContext<T> context, TableColumnModel column);
}
```

The aggregator concatenates: definition-level commands + DI-registered providers + provider
output synthesized for the current context, then applies `CanShow`, then policy, then dedups
by `Id` (last registration wins, so a feature module can override a platform command), then
orders by `(Group, Order)`. This is the requested "aggregator that hides/shows and can even
create commands": a provider is just a function of context.

### 7.3 Policy — where roles live

```csharp
public interface ITableCommandPolicy
{
    bool IsVisible(ITableCommandDescriptor command, TableCommandContext context);
}

public sealed record TableCommandContext(
    string TableId,
    int SelectionCount,
    bool IsEditable,
    object? CurrentRow,
    IReadOnlyCollection<object> SelectedRows,
    IReadOnlySet<string> Permissions);
```

Deliberately **non-generic**: a role policy is written once and reused by every table.
Default chain:

1. `PermissionTableCommandPolicy` — `command.Permission is null || permissions.Contains(...)`;
2. `SelectionTableCommandPolicy` — destructive bulk actions require a selection;
3. `CompositeTableCommandPolicy` — AND of the registered policies (all must agree).

The permission set is resolved once per table open (`ITablePermissionsProvider`, backed by
the app's auth state) and cached; policy evaluation therefore stays synchronous, which is
what menu construction needs. Auth logic never leaks into command providers.

### 7.4 Column-menu commands

```csharp
public interface IColumnMenuCommand<T> : ITableCommandDescriptor
{
    bool CanShow(TableContext<T> context, TableColumnModel column) => true;
    object? GetParameter(TableContext<T> context, TableColumnModel column) => column;
}
```

Built-in vendor items (sort, group, hide, column chooser) are **kept**, and ours are
appended. Replacing `ColumnMenu` wholesale loses them, so the wrapper customizes instead of
replaces; `PopupMenu.Opening` rebuilds the list with the built-in item instances followed by
ours. Whether the rebuilt list keeps built-in items functional is an M0 checkpoint (§14, T4).

### 7.5 Built-in grid commands

`DataGridControl.Commands` already exposes group/expand/collapse commands, and the base
type exposes export/save-layout. These are surfaced through `ITableViewPort` (§9.3) and do
not need `IRowTableCommand` wrappers — they are chrome, not application actions.

### 7.6 Menus and toolbar wiring

- `RowCellMenu.ItemsSource` is ours (starts empty), bound via `PopupMenu.Styles` to
  `Header`/`Command`/`CommandParameter` of the entry view models.
- `ColumnMenu` is extended (see 7.4).
- The toolbar is chrome: the VM exposes ordered descriptors, the view maps them to
  `ToolbarButtonItem`; non-executable items are **filtered out at build time** rather than
  left disabled, because that matches the "hide what you cannot do" requirement and avoids
  relying on `IsEnabled` binding behaviour we have not verified for every item type.
- Entries are built on `Opening` (menus) and on context-version change (toolbar). Both are
  cheap: no I/O is allowed in either path.

The entry records live in Presentation and carry icon **keys**, not images; the UI layer
resolves them through `ITableIconProvider`:

```csharp
public sealed record TableMenuEntry(
    string Header, ICommand Command, object? Parameter, string? IconKey, bool IsDestructive);

public sealed record TableToolbarEntry(
    string Header, ICommand Command, object? Parameter, string? IconKey,
    int Order, string? Group, bool IsDestructive);
```

---

## 8. Context layer

### 8.1 `TableContext<T>` is a snapshot

```csharp
public sealed record TableContext<T>(
    IReadOnlyList<T> Items,
    IReadOnlyList<T> SelectedItems,
    T? CurrentRow,
    TableColumnModel? CurrentColumn,
    int LoadedCount,
    int VisibleCount,
    int? TotalCount,
    TableSelectionMode SelectionMode,
    bool IsEditable)
{
    public bool HasSelection => SelectedItems.Count > 0;
    public bool IsSingleSelection => SelectedItems.Count == 1;
}
```

It is created when a menu opens or a command is invoked, passed as the command parameter,
and discarded. It is **not** a service and not a live object: a command that wants to
re-read the table asks the view model, not the context. This keeps commands testable with
a hand-built context. `Items` is the loaded set, not the filtered view — hot paths should
read `SelectionCount`/`VisibleCount` instead of enumerating it.

```csharp
public enum TableSelectionMode { None, Single, Multiple }
```

Three counts, because they answer three questions: `LoadedCount` (what we have),
`VisibleCount` (what filtering left; from `VisibleRowCount`), `TotalCount` (what the server
has; null until a server-paged source exists).

### 8.2 Selection and focus synchronization

`TableGrid` subscribes to `SelectionChanged` and pushes `SelectedItems`, `FocusedItem`,
`FocusedColumn` into the view model, which bumps a context version and rebuilds the
toolbar. After a live update that replaces a selected row, the wrapper re-selects by
`TableDefinition<T>.RowKey` — otherwise every refresh silently drops the user's selection.
This is a required behaviour, not a nicety.

### 8.3 Why not `IMutableContext`

The request floated editing through the context. Recommendation: **no**. Mutability is a
data-access capability, and smuggling it into a snapshot passed to every command means
every command gets write access to the source, and the "read-only table" case becomes a
convention instead of a type. Instead:

```csharp
public interface ITableMutator<T>
{
    Task<Result> AddAsync(T item, CancellationToken cancellationToken);
    Task<Result> UpdateAsync(T item, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(T item, CancellationToken cancellationToken);
}
```

`Result` does not exist in the repo yet; a minimal `TableMutationResult` record lives with
the table until a shared `Result` type is introduced. The mutator is optional (registered
only for writable sources); when absent, `TableContext<T>.IsEditable` is `false` and every
write command is filtered out by policy. Same effect as a mutable context, without the
leak.

### 8.4 In-place editing is deferred to M4

Eremex editing writes back into the row object and expects `INotifyPropertyChanged` for
live refresh. Immutable DTOs cannot support that, and wrapping rows breaks §7.1. v1
therefore edits through dialogs driven by commands; M4 introduces either mutable
per-feature DTOs or an `EditableRow<T>` adapter with an explicit commit path
(`ITableMutator<T>.UpdateAsync`). This is a scope decision, not an oversight.

---

## 9. Plugins

Three kinds, matching three different times and three different capabilities.

### 9.1 `ITableConfigurator<T>` — declarative, at definition build

```csharp
public interface ITableConfigurator<T>
{
    void Configure(TableBuilder<T> builder);
}
```

Runs once when the `TableDefinition<T>` is built. Used for cross-cutting defaults
(selection mode, row height, group panel), extra columns, and extra commands. Registered in
DI; applied in registration order.

### 9.2 `ITableBehavior<T>` — runtime, data-side

```csharp
public interface ITableBehavior<T>
{
    IDisposable Attach(TableAttachContext<T> context);
}

public sealed record TableAttachContext<T>(
    TableViewModel<T> Table,
    ITableSource<T> Source,
    ITableMutator<T>? Mutator,
    ITableCommandAggregator<T> Commands);
```

Attached on activation, disposed on deactivation. Examples: selection memory by key,
"auto-refresh every 30 s", audit logging of commands, keyboard shortcuts that are pure
view-model actions. No UI types.

### 9.3 `ITableViewPlugin<T>` — runtime, view-side

The requirement is "IViewPlugin configured from DI". The obstacle is that XAML creates the
control, so the control cannot be constructed by DI. The resolution used here:

- the plugin **interface and the port live in Presentation** (vendor-neutral);
- the implementation lives in UI and only touches the port;
- plugins travel to the control on the **DI-built view model**, because the view model is
  constructed by DI and the control reads it from DataContext.

```csharp
public interface ITableViewPort
{
    void SetRowHeight(double value);
    void ShowFilterRow(bool value);
    void ShowColumnChooser();
    void ApplySort(string field, TableSortDirection direction);
    Task ExportAsync(TableExportFormat format, Stream target, CancellationToken cancellationToken);
    Task SaveLayoutAsync(Stream target, CancellationToken cancellationToken);
    Task RestoreLayoutAsync(Stream source, CancellationToken cancellationToken);
    int VisibleRowCount { get; }
}

public enum TableExportFormat { Csv, Xlsx, Pdf }

public interface ITableViewPlugin<T>
{
    IDisposable Attach(ITableViewPort view, TableViewModel<T> table);
}
```

The `TableGrid` calls `Attach(view: this, table)` for each plugin when it attaches to the
visual tree, and disposes them on detach. DI stays a real container, no static locator, and
the vendor control type never leaks into the view model.

The default plugin set: layout persistence (`SaveLayout`/`RestoreLayout` against a settings
store keyed by `TableDefinition<T>.LayoutKey`), selection restore, export. All three are
thin wrappers over verified vendor API (§2.3).

### 9.4 Ordering and lifetimes

Definitions and configurators: singleton. Behaviors and view plugins: singleton
implementations, attached per table instance, disposed with it. Ordering: configurators by
registration order; behaviors before view plugins (data before chrome).

---

## 10. Definition, view model, view

### 10.1 `TableDefinition<T>`

```csharp
public sealed class TableDefinition<T>
{
    public required string Id { get; init; }
    public required ITableSourceFactory<T> SourceFactory { get; init; }
    public IReadOnlyList<TableColumnModel> Columns { get; init; } = [];
    public IReadOnlyList<IRowTableCommand<T>> RowCommands { get; init; } = [];
    public IReadOnlyList<IColumnMenuCommand<T>> ColumnCommands { get; init; } = [];
    public IReadOnlyList<IRowTableCommandProvider<T>> CommandProviders { get; init; } = [];
    public IReadOnlyList<ITableBehavior<T>> Behaviors { get; init; } = [];
    public IReadOnlyList<ITableViewPlugin<T>> ViewPlugins { get; init; } = [];
    public TableOptions Options { get; init; } = new();
    public Func<T, object>? RowKey { get; init; }
    public string? LayoutKey { get; init; }
}

public sealed class TableOptions
{
    public TableSelectionMode SelectionMode { get; init; } = TableSelectionMode.Single;
    public bool AutoGenerateColumns { get; init; } = true;
    public bool AllowSorting { get; init; } = true;
    public bool AllowFiltering { get; init; } = true;
    public bool ShowFilterRow { get; init; }
    public bool ShowGroupPanel { get; init; }
    public double RowHeight { get; init; } = double.NaN;   // NaN = theme default
    public string? EmptyStateText { get; init; }
}
```

Built by `TableBuilder<T>` inside an `AddTable<T>(...)` registration (§11.1), registered as
a keyed singleton.

### 10.2 `TableViewModel<T>`

```csharp
public interface ITableViewModel
{
    IEnumerable Items { get; }
    IEnumerable Columns { get; }
    object ColumnTemplate { get; }             // ITemplate<object, GridColumn>; applied in code-behind
    TableSelectionMode SelectionMode { get; }
    bool AutoGenerateColumns { get; }
    bool IsLoading { get; }
    bool IsRefreshing { get; }
    bool IsLive { get; }
    string? ErrorText { get; }
    string? EmptyText { get; }
    IReadOnlyList<TableToolbarEntry> ToolbarEntries { get; }
    IReadOnlyList<TableMenuEntry> BuildRowMenu(object? row);
    IReadOnlyList<TableMenuEntry> BuildColumnMenu(TableColumnModel column);
    IDisposable Activate();
}

public sealed class TableViewModel<T> : ViewModelBase, ITableViewModel, IAsyncDisposable
{
    public ReadOnlyObservableCollection<T> Rows { get; }
    public IReadOnlyList<TableColumnModel> Columns { get; }
    public TableContext<T> Context { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
}
```

`object ColumnTemplate` in the non-generic surface is the pragmatic price of XAML not
supporting generic controls; the typed property stays on `TableViewModel<T>`. The cast
happens once, in `TableGrid.axaml.cs`, with a clear invalid-cast message.

`ITableViewModel` exists so that `TableGrid.axaml` can use compiled bindings with a
concrete `x:DataType` — no reflection bindings, no `dynamic`.

### 10.3 `TableGrid`

`TableGrid : UserControl, ITableViewPort`, DataContext = `ITableViewModel`.

Responsibilities:

1. `ItemsSource="{Binding Items}"` set **exactly once** per instance (re-assignment
   regenerates auto-generated columns — §2.1);
2. `ColumnsSource="{Binding Columns}"` and `AutoGenerateColumns="{Binding AutoGenerateColumns}"`
   from XAML; `ColumnTemplate` is assigned in code-behind
   (`grid.ColumnTemplate = vm.ColumnTemplate as ITemplate<object, GridColumn>`) because the
   non-generic interface carries it as `object` and a compiled binding cannot convert it;
3. wrap collection bursts in `BeginDataUpdate`/`EndDataUpdate` (via a collection-changed
   observer with a short debounce);
4. translate `SelectionMode` from `TableSelectionMode` to `RowSelectionMode`;
5. build menus on `Opening`, toolbar on context change;
6. call `Activate()` on `OnAttachedToVisualTree`, dispose on detach;
7. attach view plugins, resolve cell templates through the catalog.

Everything else — templates, editors, icons — is delegated to catalogs.

### 10.4 Activation

`TableViewModel<T>.Activate()` returns an `IDisposable` that owns the source subscription,
the behavior attachments and the plugin attachments. Disposing it disconnects the source
(RefCount drops to zero), which is what stops the SignalR subscription and the SSE reader.
This must be the only lifetime mechanism; a table that is removed from the tree must stop
streaming without the screen doing anything.

---

## 11. DI composition

### 11.1 Registration API

Per the house rule (flat `Add<Layer>()` per client project, composition only in the host):

```csharp
// Mirai.Space.Presentation
services.AddTables(tables =>
{
    tables.AddTable<OrderRow>("orders", table => table
        .Source<OrdersTableSourceFactory>()
        .Options(o => o
            .Selection(TableSelectionMode.Multiple)
            .Filtering()
            .PersistLayout("orders"))
        .Columns(columns => columns
            .Override("Number", c => c.Header = "№").Width(TableColumnWidth.Px(120))
            .Override("Status", c => c.CellTemplateKey = "OrderStatusCell"))
        .RowCommand<DeleteOrdersCommand>()
        .ColumnMenuCommand<CopyFieldCommand>()
        .Behavior<SelectionMemoryBehavior<OrderRow>>());
});

// Mirai.Space.UI
services.AddTableViews(views =>
{
    views.Template("OrderStatusCell", new DataTemplate(() => ...));
    views.ViewPlugin<LayoutPersistencePlugin<OrderRow>>();
});
```

Host chain becomes `...AddPresentation().AddTables().AddUi().AddTableViews()...` — two more
links, no new cross-layer calls.

### 11.2 Getting a table into a screen

Two styles; A is the default.

**A. Factory** (needed whenever the table depends on screen arguments):

```csharp
public sealed class OrdersViewModel : ViewModelBase
{
    public TableViewModel<OrderRow> Table { get; }

    public OrdersViewModel(ITableFactory factory, IScreenParameters parameters) =>
        Table = factory.Create<OrderRow>("orders",
            new TableOpenContext("orders", new Dictionary<string, object?>
            {
                [OrderArgs.CustomerId] = parameters.CustomerId,
            }));
}
```

`IScreenParameters` stands for whatever context service the screen already has — it is not
part of the table component and is shown only to make the argument flow explicit.

**B. Keyed injection** for fixed tables with no arguments:

```csharp
public OrdersViewModel([FromKeyedServices("orders")] TableViewModel<OrderRow> table) => Table = table;
```

Trade-off: B is terser and keeps DI honest, A is the only option when the screen resupplies
the query. Both register the same way; `AddTable` registers the definition as a singleton
and the view model as a keyed transient factory.

### 11.3 Why a definition and a view model are separate

The definition is shared, immutable, and testable without a UI; the view model owns
subscriptions and collections and dies with the screen. Merging them would make the
definition stateful and force a singleton to hold a `CancellationTokenSource`. The split
also enables one definition → several simultaneous tables (two order lists side by side).

---

## 12. Decisions (ADR-lite)

| # | Decision | Rationale | Rejected alternative |
| --- | --- | --- | --- |
| D1 | Rows are DTOs, never row view models | Eremex `FieldName` resolves against the row object; wrappers duplicate every property and lose selection on every live update | `RowViewModel<T>` with commands |
| D2 | Commands are table-scoped, parameterized by `TableContext<T>` | One instance, testable, no per-row allocation | Per-row command instances |
| D3 | `ITableSource<T>` has no `Reset` | Re-targeting is a new definition + new source | `Reset(newQuery)` on the source |
| D4 | Snapshot and feed are separate interfaces, composed by default | Real tables need both; transports stay one-interface thin | A single `ITableSource` per transport |
| D5 | Presentation has zero Avalonia/Eremex references | Keeps the engine testable and vendor-swappable; forces the port | Bind the grid directly to view models with vendor types |
| D6 | Templates resolve by key through a catalog | Controls are XAML-created, DI cannot inject them; keys keep Presentation clean | Static service locator; per-type templates in `TableGrid.axaml` |
| D7 | No `IMutableContext`; optional `ITableMutator<T>` | Mutability is a capability, not a snapshot | Writable context passed to every command |
| D8 | Policies are non-generic over a coarse context | One role policy for all tables | `ITableCommandPolicy<T>` per table type |
| D9 | Menus filtered at build time, not disabled | Matches "hide what you cannot do"; no reliance on unverified `IsEnabled` bindings | Build all items, rely on `CanExecute` |
| D10 | `TableGrid` is non-generic; generic engine sits in the VM | XAML cannot instantiate a generic control | `TableGrid<T>` with a custom control factory |
| D11 | Activation is explicit and disposable | Sources must stop when the view leaves the tree | Fire-and-forget connect in the constructor |

---

## 13. Traps and open risks

| # | Risk | Mitigation |
| --- | --- | --- |
| T1 | The grid materializes everything and filters locally; a server-paged table (10k+) needs a different data path | v1 documents the materialized mode; M4 adds `ITableQuery` (page, sort, filter) on `TableOpenContext` + a query observer pushing changes to the source |
| T2 | Re-assigning `ItemsSource` deletes and regenerates auto-generated columns, wiping widths/order | Assign once per `TableGrid` instance; refresh through the collection, never through `ItemsSource` |
| T3 | Ordering between `ColumnsSource` columns and auto-generated columns is unspecified | M0 checkpoint; fallback = reorder by our own `VisibleIndex` after `AutoGeneratedColumns` |
| T4 | Rebuilding `ColumnMenu.ItemsSource` on `Opening` may break built-in items | M0 checkpoint: confirm built-in item instances survive the rebuild, otherwise append instead of rebuild |
| T5 | DynamicData keyed change sets vs. the unkeyed `ITableSource<T>` contract | Isolated adapter; M0 proves it with a 50-line spike, fallback = `SourceList<T>` + dictionary index |
| T6 | Selection lost on live updates | Restore by `TableDefinition<T>.RowKey` after each batch |
| T7 | Eremex version drift / licensing | Single version for the whole `Eremex.*` family via CPM; vendor feed credentials never committed |
| T8 | Theme not registered before the first control → unstyled grid at runtime | Register the theme dictionary in `App.axaml` before any view is created; smoke test in M0 |
| T9 | Missing cell template fails silently | Hard failure in Debug + visible placeholder in Release |
| T10 | Policy needs async I/O (permission checks) but menus build synchronously | Resolve permissions once per open, cache, evaluate synchronously |

---

## 14. Milestones

**M0 — spike (no production code).** Add the Eremex feed and one grid on a hand-written
VM. Prove: theme loads; `ColumnsSource` + `ColumnTemplate` + `AutoGenerateColumns=true`
interleave (T3); `ItemsSource` set once + DD `Bind` survives 500-row snapshots; keyed
change-set adapter works (T5); `SelectionChanged` → context; one `RowCellMenu` item
executes a command with a context parameter; `ColumnMenu` rebuild keeps built-ins (T4);
`SaveLayout`/`RestoreLayout` round-trips. Output: one `.axaml` + one `.cs` in a throwaway
view, plus a findings note appended to `GOTCHAS.md`.

**M1 — data + read-only grid.** `ITableSource<T>`, the four shipped sources, DD bind,
selection sync, states (loading/empty/error/stale), `TableGrid`, `TableViewModel<T>`,
`AddTables`, one screen wired end to end (a demo screen is acceptable — no production table
screen exists yet).

**M2 — commands.** Descriptors, providers, aggregator, policies, row/column menus,
toolbar, context snapshot, permission cache.

**M3 — shape + plugins.** Column overrides, `TableColumnTemplate`, template catalog,
icon provider, layout persistence, export, selection memory, behavior/view-plugin
lifecycle.

**M4 — write path.** `ITableMutator<T>`, edit dialogs, in-place editing decision, server
paging (`ITableQuery`), grouping counts.

Each milestone ends with `dotnet build` green, the affected screen working, and a note in
`DECISIONS.md` for anything that changed from this document.

---

## 15. Open questions

1. **Server paging in the near term?** If yes, `ITableQuery` belongs in `TableOpenContext`
   from M1, not M4 — it is cheap now and expensive later.
2. **Row detail / expandable row** needed anywhere? Eremex support for row details is
   unverified; if the answer is yes it becomes an M0 spike item because it may change the
   `TableGrid` template structure.
3. **Multiple tables per screen** with linked selection (master-detail)? This decides
   whether `ITableFactory` needs a parent/child link or whether screens wire it manually.
4. **Where do `Result` errors surface** — grid-level banner, command-level toast, or both?
   The pattern must be uniform before M2.
5. **Is a second vendor adapter plausible** in 12 months (Avalonia DataGrid community
   edition, for example)? If yes, the port must stay minimal and vendor-neutral; if no,
   the port can absorb more Eremex-specific convenience.
