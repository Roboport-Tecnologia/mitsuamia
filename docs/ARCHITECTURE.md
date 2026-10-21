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
- **macOS's current controls need the current SDK.** AppKit gives an app
  macOS 26's controls only if its executable was linked against the 26
  SDK; .NET's prebuilt app host is linked against 15.5, so SourceGit first
  showed macOS 15's controls. `buildTransitive/Mitsuamia.targets` marks the
  app host after each build and publish as linked against the installed
  SDK (`vtool`) and signs it again ad hoc. NativeAOT links the executable
  with the installed SDK, so it needs nothing. Checked by eye on macOS 26
  (2026-10-21): the marked SourceGit shows macOS 26's controls.

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
| `Resources` (themes, styles, icons) | 17k lines of `.axaml` | Dropped, except its icons |

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
- **Toolbars stay upstream's band,** not the window's toolbar. A window's
  toolbar is the native place for a page's buttons, but the repository
  page's band has far more buttons than a toolbar holds (and toolbars only
  share trailing items), so both pages keep upstream's 36-point band:
  borderless icon buttons in 32-point slots, each icon padded in its view
  box to upstream's size and nudge (`Icons.Get(key, size, down)`), so they
  line up as upstream's do.
- **Captures for checking the look:** `SOURCEGIT_CAPTURE=<file.png>`
  (with `SOURCEGIT_CAPTURE_TAB=new` for a welcome page) writes the main
  window's content to a PNG once it settles, through mitsuami's offscreen
  capture, which needs no screen-recording permission.

### What it needs

- **In mitsuami** (if every platform has it natively): a tree (branches,
  the welcome page's groups, changed files), text with inline runs (116
  `Run`s), popovers (`Popup`, `MenuFlyout`).
- **In mitsuami-extras:** a resizable split (WinUI 3 has no native
  splitter); the commit graph and charts, drawn; the avatar.
- **The code view is an embedded bemtvi** (`../nxvim`), for diffs, blame,
  the merge conflict editor and the commit message, on every platform.
  `bemtvi-gui` already runs on mitsuami (a `GpuSurface` painted with wgpu);
  what embedding needs:
  - a widget, not a window: an editor surface several of which share a
    window (a side-by-side diff, the conflict editor's three panes), from a
    crate in bemtvi's repository with a C ABI of its own;
  - the server in process, one per app, with a session per surface;
  - diffs through the `bemtvi-diff` plugin, a Meld-style renderer you feed
    a diff to: 2 or 3 read-only panes in lockstep, tints and intra-line
    spans as extmarks, hunk navigation, and conflict resolution (ours,
    theirs, both, picked lines), which covers SourceGit's conflict editor.
    It diffs whole sides itself (LCS); SourceGit's diff comes from git with
    its options (whitespace, context lines), and staging lines turns
    selected lines into a patch for git's hunks. So its spec should also
    take a diff already made (git's hunks), with the selection reported
    back. It has one session at a time, and side-by-side only: SourceGit
    also shows a unified diff;
  - a modeless mode for people who don't use vim, coming to bemtvi's core,
    and read-only buffers for diffs and blame;
  - accessibility, later: a `GpuSurface` shows screen readers nothing.
    SourceGit's own code views don't either (AvaloniaEdit has no automation
    peers), so it isn't a step back.
- **Accessibility gets better for free.** SourceGit sets no
  `AutomationProperties` and none of its 25 drawn controls has an
  automation peer; its 125 or so icon-only buttons likely read as unnamed.
  On mitsuami, native controls carry their roles, and a button's label is
  its accessible name even when only its icon shows.
- **Icons:** SourceGit's own, its 171 vector icons (`Resources/Icons.axaml`,
  path data on a 1024 grid), not the platforms' sets. Every platform shows
  an app's vector icon in its icon colour natively: WinUI's `PathIcon` takes
  the same path data, GTK recolours symbolic SVGs, Kirigami's `Icon` an SVG
  mask, AppKit template images. So it's an addition to mitsuami's `Icon`
  (an icon the app gives, next to named ones), not an extra.

## 5. Plan

1. **Proof of concept:** the C ABI in mitsuami, the C# layer, the MVVM bridge
   and the dispatcher, on macOS. SourceGit's `Welcome` page with its real
   view model.
   - Done: `samples/Clone`, a form on a CommunityToolkit.Mvvm view model,
     ran on AppKit and was checked by eye (2026-10-21): typing a URL
     enables the button through `CanExecute`, and the command's
     background work reports back on the UI thread. Windows and Linux
     only type-check, on mitsuami's side.
2. **The launcher:** welcome page, repository tabs, opening a repository,
   clone, preferences. Brings the tree, the first popups and the app's icons.
3. **The repository view:** history (`Table` and the drawn graph), local
   changes, committing, branches, the split.
4. **Diffs, blame and conflicts:** the code view, the largest single piece.
5. **The remaining popups and windows,** the converter making the first pass.

**The C ABI sits on the core.** mitsuami's widgets are thin wrappers over
the core's `Element` (a kind, props, style, handlers, children), and the
core keeps `Changed` events' values itself (`absorb`, in `ui/pipeline.rs`).
The ABI drives `Ui` directly: `create(kind, props)`, `set_prop`,
`update_style`, `append_child`, `on_event`, `destroy`, and a window's
`create_window`. What's more than props gets entry points of its own:
lists' and tables' rows (asked for as they scroll), menus, tabs, and
mitsuami's reactive `Show` and `For`, which C# replaces with its own
collection changes.
