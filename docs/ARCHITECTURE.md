# Architecture

## 1. Goals

- **C# apps on every desktop platform with native controls.** A Windows
  developer's app keeps WinUI 3 on Windows and gains AppKit on macOS and
  GTK 4 or Kirigami on Linux. Avalonia gets there by drawing its own
  controls; mitsuamia gets there with each platform's.
- **Apps keep their view models.** Avalonia and WPF apps are MVVM: their
  models, view models and logic stay; their views are replaced.
- **Native always wins,** as in mitsuami. What has no native control on some
  platform comes from mitsuami-extras, which decides what each platform shows.

## 2. Layers

```
 C# app (view models, models)          e.g. SourceGit
 ─────────────────────────────
 mitsuamia (C#)                        controls as classes, MVVM bridge,
                                       dispatcher, handles
 ─────────────────────────────
 C ABI      mitsuami        mitsuami-extras
 ─────────────────────────────
 mitsuami core and backends (Rust)
```

- **The C ABI is data-driven,** over the core's node tree: create a node of a
  kind, set a prop, insert and remove children, subscribe to events, run the
  app, post to the UI thread. mitsuami's builder API (typestate generics,
  closures, `view!`) can't cross a C ABI, so it isn't bound. Each repository
  exports its own: mitsuami its core and widgets, mitsuami-extras its
  widgets.
- **The C# layer** is hand-written: a class per widget with properties and
  events, a `Window`, and the app.
- **The MVVM bridge** binds `INotifyPropertyChanged` properties to props and
  `ICommand`s to events, so view models (CommunityToolkit.Mvvm's included)
  work unchanged.
- **Threading:** mitsuami's `run` takes the main thread (AppKit needs it; a
  .NET `Main` runs on it). A `SynchronizationContext` posts to mitsuami's
  loop, so `await` resumes on the UI thread.
- **Handles:** Rust nodes are disposed with their scope, C# objects when the
  GC collects them, so handles carry a generation and a stale one fails
  cleanly. Panics are caught at the boundary, and so are exceptions in
  callbacks.
- **Escape hatches:** a raw native handle is wrapped with each platform's
  .NET bindings (C#/WinRT for WinUI, the .NET macOS bindings, Gir.Core for
  GTK).
- **Packaging:** a NuGet package with a native library per runtime
  identifier; Linux has two, GTK and Kirigami.

## 3. The XAML converter

A one-shot migration tool, not a runtime loader: it turns Avalonia (later
WPF) `.axaml` into mitsuamia C# views, and reports what it dropped.

- Elements map to widgets (`TextBlock` to `Text`, `StackPanel` to `Column`
  or `Row`, `Grid` definitions to grid tracks, `DataGrid` to `Table`).
- `{Binding}`s map to the bridge: two-way on editable props, one-way
  elsewhere; `Command` to events; converters and `StringFormat` to TODOs.
- Styles, themes, templates and brushes are dropped: native controls can't
  be restyled (mitsuami §5).

## 4. SourceGit, the stress test

SourceGit (MIT, a Git client on Avalonia 11.3, .NET 10) is ported in place,
in `Roboport-Tecnologia/sourcegit` on the `mitsuami` branch, so upstream
releases can still be merged. Surveyed at `cb11d687` (v2026.22):

| Part | Size | Plan |
|---|---|---|
| `Commands` (git), `Models`, `AI` | ~12k lines | Kept. `Commands` doesn't use Avalonia |
| `ViewModels` | 136 files, ~22k lines | Kept, with small changes |
| `Views` | 148 `.axaml` (18k lines), 183 `.cs` (27k lines) | Replaced |
| `Resources` (themes, styles, icons) | 17k lines of `.axaml` | Dropped; icons mapped |

- **View models use Avalonia** in 45 files, mostly for
  `Avalonia.Threading` (28, the dispatcher), `Avalonia.Collections` (19,
  `AvaloniaList`, to become `ObservableCollection`) and `Bitmap` (7). 59
  classes are CommunityToolkit `ObservableObject`s.
- **The code-behind is half the views:** 27k lines of C#, which the converter
  doesn't touch. 25 controls draw themselves; the largest are the text diff
  view (1.7k lines), the branch tree (1.5k), the merge conflict editor, the
  commit message box and the commit graph.
- **What the views use,** by count: `TextBlock` 1098, `Path` 578 (icons),
  `Grid` 490, `StackPanel` 439, `Border` 326, `Button` 232, `TextBox` 104,
  `CheckBox` 96, `ComboBox` 37, `ListBox` 33, `TabItem` 32, `MenuItem` 32,
  `ToggleButton` 25, `GridSplitter` 17, `RadioButton` 11, `TabControl` 9,
  `TextEditor` (AvaloniaEdit) 8, `MenuFlyout` 7, `Popup` 4, `DataGrid` 1.
- **70 popups** (clone, create branch, add remote, …) show inside the
  repository view in Avalonia. They become window-modal sheets and dialogs
  (mitsuami §14.1), as each platform's Git clients show them.
- **The launcher's repository tabs** are mitsuami's document tabs (§14.13).

### What it needs

- **In mitsuami** (if every platform has it natively): a tree (branches,
  the welcome page's groups, changed files), text with inline runs (116
  `Run`s), popovers (`Popup`, `MenuFlyout`).
- **In mitsuami-extras:** a resizable split (WinUI 3 has no native
  splitter); a code view for diffs, blame and conflicts (GtkSourceView and
  KTextEditor are native, macOS and Windows have none); the commit graph and
  charts, drawn; the avatar.
- **Icons:** the themes' vector icons mapped to SF Symbols, Segoe Fluent and
  freedesktop names.

## 5. Plan

1. **Proof of concept:** the C ABI in mitsuami, the C# layer, the MVVM bridge
   and the dispatcher, on macOS. SourceGit's `Welcome` page with its real
   view model.
2. **The launcher:** welcome page, repository tabs, opening a repository,
   clone, preferences. Brings the tree, the first popups and the icon map.
3. **The repository view:** history (`Table` and the drawn graph), local
   changes, committing, branches, the split.
4. **Diffs, blame and conflicts:** the code view, the largest single piece.
5. **The remaining popups and windows,** the converter making the first pass.

The first question step 1 answers: whether the core's data-driven layer is
enough for the C ABI, or whether it has to go through `mitsuami-widgets`
(widgets' accessibility defaults, `absorb` for their `Changed` events).
