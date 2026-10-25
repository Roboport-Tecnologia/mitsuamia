using System.Collections;
using System.Windows.Input;

namespace Mitsuamia;

public enum TextStyle : uint { LargeTitle, Title, Headline, Body, Callout, Caption, Monospace }

public enum ButtonRole : uint { Normal, Default, Cancel, Destructive }

/// <summary>How a button is drawn. <c>Prominent</c>: filled with the accent
/// colour, as the platform marks the action it suggests, without taking
/// Return as the window's default button does (a plain button where the
/// platform has none: KDE, a macOS split button).</summary>
public enum ButtonStyle : uint { Automatic, Bordered, Borderless, Prominent }

public enum HeaderEdge : uint { Automatic, Soft, Hard }

/// <summary>What a container takes when dropped: folders, and files with
/// these extensions (without the dot), or any file with none given.</summary>
public sealed record FileDrop(bool Folders, IReadOnlyList<string>? Extensions = null)
{
    public static FileDrop OnlyFolders { get; } = new(true, []);
}

/// <summary>A flexbox container, a column by default. Children go in with a
/// collection initializer: <c>new Column { a, b }</c>.</summary>
public class Container : Node, IEnumerable<Node>
{
    public Container() : base(Native.Kind.Container) { }

    /// <summary>One mitsuami made already: a scroll view's content.</summary>
    internal Container(uint id) : base(id) { }

    private protected Container(Native.Kind kind) : base(kind) { }

    public void Add(Node child) => base.Add(child);
    public void Insert(int index, Node child) => base.Add(child, index);

    public Length Gap { set => Native.Check(Native.SetGap(Id, value)); }

    /// <summary>Children that don't fit go on to another line (a row's) or
    /// column (a column's), as chips and tags do.</summary>
    public bool Wrap { set => Native.Check(Native.SetWrap(Id, value)); }

    public Thickness Padding
    {
        set => Native.Check(Native.SetPadding(Id, value.Top, value.End, value.Bottom, value.Start));
    }

    public Align AlignItems { set => Native.Check(Native.SetAlignItems(Id, (uint)value)); }
    public Justify Justify { set => Native.Check(Native.SetJustify(Id, (uint)value)); }

    /// <summary>Its window's header: on macOS a strip in the title bar,
    /// under the title and spanning the window, as Safari's tab bar; on the
    /// others the content's top, as it is.</summary>
    public bool IsHeader { set => Native.Check(Native.SetBool(Id, Native.BoolProp.Header, value)); }

    /// <summary>A pane down its window's side that navigates, built by the
    /// app (the rail that picks a Git client's pages): on Windows, under tabs in the title
    /// bar, on the same layer as the chosen tab and the header; elsewhere
    /// as it is.</summary>
    public bool IsSidebarPane { set => Native.Check(Native.SetBool(Id, Native.BoolProp.SidebarPane, value)); }

    /// <summary>How a header meets what scrolls under it, on macOS 26.1 and
    /// later.</summary>
    public HeaderEdge HeaderEdge { set => Native.Check(Native.SetEnum(Id, Native.EnumProp.HeaderEdge, (uint)value)); }

    /// <summary>Takes folders and files dropped on it from the platform's
    /// file manager: see <see cref="FileDrop"/>. Not reachable without a
    /// pointer, so offer another way too.</summary>
    public FileDrop? FileDrop
    {
        set => Native.Check(Native.SetFileDrop(Id, value?.Folders ?? false, value is { Extensions: null },
            value?.Extensions ?? []));
    }

    /// <summary>Files it takes are over it, or have left: for a highlight.</summary>
    public event Action<bool>? DropHover;

    /// <summary>The paths dropped on it that it takes.</summary>
    public event Action<IReadOnlyList<string>>? FilesDropped;

    private Action? _clicked;

    /// <summary>It, or a label or icon inside it, was clicked (a control
    /// inside keeps its clicks): a header opening its section, as GTK's and
    /// WinUI's expanders. Pointers only: give the keyboard a control inside
    /// too.</summary>
    public event Action? Clicked
    {
        add
        {
            if (_clicked is null) Native.Check(Native.SetBool(Id, Native.BoolProp.Clickable, true));
            _clicked += value;
        }
        remove
        {
            _clicked -= value;
            if (_clicked is null) Native.Check(Native.SetBool(Id, Native.BoolProp.Clickable, false));
        }
    }

    private protected override unsafe void OnEvent(Native.Event e)
    {
        switch (e.Kind)
        {
            case Native.EventKind.Click:
                _clicked?.Invoke();
                break;
            case Native.EventKind.PopoverShownChanged when this is Popover popover:
                popover.Dismissed();
                break;
            case Native.EventKind.DropHover:
                DropHover?.Invoke(e.Flag);
                break;
            case Native.EventKind.FilesDropped:
                FilesDropped?.Invoke(Native.Utf8(e.Text, e.TextLen).Split('\0', StringSplitOptions.RemoveEmptyEntries));
                break;
        }
    }

    public IEnumerator<Node> GetEnumerator() => Children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// The platform's popover (an NSPopover on macOS, a popover on GNOME, a
/// flyout on Windows, a popup on KDE), shown by its parent while
/// <see cref="IsShown"/>. Add it to the container it points at, after what
/// that shows; it takes no room there. Its children are laid out as a
/// column's, at their natural size, and the platform places it by its
/// parent. The user dismisses it (a click outside, Escape):
/// <see cref="Closed"/>, and it's no longer shown.
/// </summary>
public sealed class Popover : Container
{
    private bool _shown;

    public Popover() : base(Native.Kind.Popover) => Native.Check(Native.SetDirection(Id, false));

    public bool IsShown
    {
        get => _shown;
        set
        {
            _shown = value;
            Native.Check(Native.SetBool(Id, Native.BoolProp.PopoverShown, value));
        }
    }

    /// <summary>The user dismissed it.</summary>
    public event Action? Closed;

    internal void Dismissed()
    {
        _shown = false;
        Closed?.Invoke();
    }
}

public class Column : Container
{
    public Column() => Native.Check(Native.SetDirection(Id, false));
}

public class Row : Container
{
    public Row() => Native.Check(Native.SetDirection(Id, true));
}

public enum Modality : uint { Window, Application }

public sealed class Window : Node
{
    /// <summary>A window of this content size; without a height, it
    /// follows its content's height, as a dialog does.</summary>
    public Window(string title, float width, float? height = null)
        : base(Native.CheckNode(CreateWindow(title, width, height ?? -1))) { }

    /// <summary>A modal window on <paramref name="owner"/>: a sheet on
    /// macOS with <see cref="Modality.Window"/>. Escape or its close button
    /// close it.</summary>
    public Window(Window owner, string title, float width, float? height = null, Modality modality = Modality.Window)
        : this(title, width, height) =>
        Native.Check(Native.SetModal(Id, owner.Id, (uint)modality));

    private static unsafe uint CreateWindow(string title, float width, float height)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(title);
        fixed (byte* p = bytes) return Native.CreateWindow(p, (nuint)bytes.Length, width, height);
    }

    public string Title { set => Native.Check(Native.SetText(Id, Native.TextProp.Title, value)); }

    public Node Content
    {
        set => Add(value);
    }

    /// <summary>Captures its content offscreen, as the platform draws it, to
    /// a PNG file in pixels: for tests and tools, with no screen-recording
    /// permission. True if it was written.</summary>
    public unsafe Task<bool> CapturePng(string path)
    {
        var done = new TaskCompletionSource<bool>();
        var user = System.Runtime.InteropServices.GCHandle.ToIntPtr(System.Runtime.InteropServices.GCHandle.Alloc(done));
        Native.Check(Native.CapturePng(Id, path, &Captured, user));
        return done.Task;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void Captured(nint user, byte ok)
    {
        var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr(user);
        var done = (TaskCompletionSource<bool>)handle.Target!;
        handle.Free();
        done.SetResult(ok != 0);
    }

    /// <summary>The user closed it; mitsuami has destroyed it.</summary>
    public event Action? Closed;

    private protected override void OnEvent(Native.Event e)
    {
        if (e.Kind != Native.EventKind.CloseRequested) return;
        Forget();
        Closed?.Invoke();
    }
}

public sealed class Text : Node
{
    public Text(string text = "") : base(Native.Kind.Text) => Value = text;

    public string Value { set => Native.Check(Native.SetText(Id, Native.TextProp.Text, value)); }
    public TextStyle Style { set => Native.Check(Native.SetEnum(Id, Native.EnumProp.TextStyle, (uint)value)); }

    public Color Color { set => Native.Check(value.Set(Id)); }

    /// <summary>Its weight, over its style's.</summary>
    public FontWeight Weight { set => Native.Check(Native.SetEnum(Id, Native.EnumProp.FontWeight, (uint)value)); }

    /// <summary>Shows at most this many lines, the last cut off with the
    /// platform's ellipsis; 0 shows them all.</summary>
    public int MaxLines { set => Native.Check(Native.SetNumber(Id, Native.NumberProp.MaxLines, value)); }

    /// <summary>The user can select and copy it. Set before it's shown: a
    /// platform may not change it afterwards.</summary>
    public bool Selectable { set => Native.Check(Native.SetBool(Id, Native.BoolProp.Selectable, value)); }
}

public enum FontWeight : uint { Regular, Medium, Semibold, Bold }

/// <summary>Takes an <see cref="ICommand"/>, as Avalonia's and WPF's
/// buttons do: clicking executes it, and it's enabled while it can.</summary>
public sealed class Button : Node
{
    private ICommand? _command;

    public Button(string label = "") : base(Native.Kind.Button) => Label = label;

    public string Label { set => Native.Check(Native.SetText(Id, Native.TextProp.Label, value)); }
    public ButtonRole Role { set => Native.Check(Native.SetEnum(Id, Native.EnumProp.ButtonRole, (uint)value)); }
    public ButtonStyle Style { set => Native.Check(Native.SetEnum(Id, Native.EnumProp.ButtonStyle, (uint)value)); }

    /// <summary>The app's own icon before its caption.</summary>
    public VectorIcon? Icon { set => Native.Check(Native.SetVectorIcon(Id, value)); }

    /// <summary>An icon from the platform's own set, by its name there.</summary>
    public string IconName { set => Native.Check(Native.SetText(Id, Native.TextProp.Icon, value)); }

    /// <summary>Shows only its icon; the caption stays its accessible
    /// name, and makes a good tooltip.</summary>
    public bool IconOnly { set => Native.Check(Native.SetBool(Id, Native.BoolProp.IconOnly, value)); }

    /// <summary>Its icon's colour, in place of the platform's; the caption
    /// keeps the platform's.</summary>
    public Color IconColor { set => Native.Check(value.Set(Id)); }

    public event Action? Click;

    public ICommand? Command
    {
        set
        {
            if (_command is not null) _command.CanExecuteChanged -= CanExecuteChanged;
            _command = value;
            if (value is not null) value.CanExecuteChanged += CanExecuteChanged;
            CanExecuteChanged(null, EventArgs.Empty);
        }
    }

    public object? CommandParameter { get; set; }

    private void CanExecuteChanged(object? sender, EventArgs e) =>
        IsEnabled = _command?.CanExecute(CommandParameter) ?? true;

    private protected override void OnEvent(Native.Event e)
    {
        if (e.Kind != Native.EventKind.Click) return;
        Click?.Invoke();
        if (_command?.CanExecute(CommandParameter) == true) _command.Execute(CommandParameter);
    }
}

public enum Orientation : uint { Horizontal, Vertical }

/// <summary>The platform's line between groups of content, across its
/// container (horizontal) or, vertical, between items in a row.</summary>
/// <summary>
/// The platform's button that opens a menu of actions (a pull-down on
/// macOS, a menu button on GNOME, a drop-down button on Windows), with a
/// <see cref="Button"/>'s caption, icon and style.
/// </summary>
public sealed class MenuButton : Node
{
    private Dictionary<uint, Action>? _actions;

    public MenuButton(string label = "") : base(Native.Kind.MenuButton) => Label = label;

    public string Label { set => Native.Check(Native.SetText(Id, Native.TextProp.Label, value)); }
    public ButtonStyle Style { set => Native.Check(Native.SetEnum(Id, Native.EnumProp.ButtonStyle, (uint)value)); }

    /// <summary>The app's own icon before its caption.</summary>
    public VectorIcon? Icon { set => Native.Check(Native.SetVectorIcon(Id, value)); }

    /// <summary>Shows only its icon; the caption stays its accessible name.</summary>
    public bool IconOnly { set => Native.Check(Native.SetBool(Id, Native.BoolProp.IconOnly, value)); }

    /// <summary>Its icon's colour, in place of the platform's.</summary>
    public Color IconColor { set => Native.Check(value.Set(Id)); }

    /// <summary>What it opens; each item runs its action when chosen.</summary>
    public IReadOnlyList<MenuEntry> Menu
    {
        set
        {
            var (flat, actions) = FlattenMenu(value);
            Native.Check(Native.SetMenu(Id, flat));
            _actions = actions;
        }
    }

    private Func<IReadOnlyList<MenuEntry>>? _menuOnOpen;

    /// <summary>Builds its menu each time the platform opens it, for a
    /// menu that depends on what's true then; it reads what it needs then.
    /// Null: the menu is <see cref="Menu"/>'s.</summary>
    public unsafe Func<IReadOnlyList<MenuEntry>>? MenuOnOpen
    {
        set
        {
            _menuOnOpen = value;
            Native.Check(value is null
                ? Native.SetMenuOnOpen(Id, null, 0)
                : Native.SetMenuOnOpen(Id, &BuildMenu, Handle));
        }
    }

    /// <summary>The platform is opening the menu: the function builds it,
    /// and it's set as the menu.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void BuildMenu(nint user, uint id)
    {
        if (System.Runtime.InteropServices.GCHandle.FromIntPtr(user).Target is not MenuButton { _menuOnOpen: { } build } button) return;
        Application.Guard(() => button.Menu = build());
    }

    private protected override void OnEvent(Native.Event e)
    {
        if (e.Kind == Native.EventKind.MenuItem && _actions?.GetValueOrDefault((uint)e.Index) is { } action)
            action();
    }
}

/// <summary>
/// The platform's button with a menu of other actions beside its own (a
/// combo button on macOS, a split button on GNOME and Windows; on KDE, an
/// arrow button joined to it), with a <see cref="Button"/>'s caption, icon,
/// style and click.
/// </summary>
public sealed class SplitButton : Node
{
    private Dictionary<uint, Action>? _actions;

    public SplitButton(string label = "") : base(Native.Kind.SplitButton) => Label = label;

    public string Label { set => Native.Check(Native.SetText(Id, Native.TextProp.Label, value)); }
    public ButtonStyle Style { set => Native.Check(Native.SetEnum(Id, Native.EnumProp.ButtonStyle, (uint)value)); }

    /// <summary>The app's own icon before its caption.</summary>
    public VectorIcon? Icon { set => Native.Check(Native.SetVectorIcon(Id, value)); }

    /// <summary>Shows only its icon; the caption stays its accessible name.</summary>
    public bool IconOnly { set => Native.Check(Native.SetBool(Id, Native.BoolProp.IconOnly, value)); }

    public event Action? Click;

    /// <summary>What its arrow opens; each item runs its action when
    /// chosen.</summary>
    public IReadOnlyList<MenuEntry> Menu
    {
        set
        {
            var (flat, actions) = FlattenMenu(value);
            Native.Check(Native.SetMenu(Id, flat));
            _actions = actions;
        }
    }

    private protected override void OnEvent(Native.Event e)
    {
        if (e.Kind == Native.EventKind.Click)
            Click?.Invoke();
        else if (e.Kind == Native.EventKind.MenuItem && _actions?.GetValueOrDefault((uint)e.Index) is { } action)
            action();
    }
}

public sealed class Separator : Node
{
    public Separator(Orientation orientation = Orientation.Horizontal) : base(Native.Kind.Separator) =>
        Orientation = orientation;

    public Orientation Orientation
    {
        set => Native.Check(Native.SetEnum(Id, Native.EnumProp.Orientation, (uint)value));
    }
}

/// <summary>
/// Two panes with a divider between them that the user drags, as the
/// platform splits a window: side by side, or one over the other when
/// <see cref="Orientation"/> is vertical. Its two children are the panes.
/// The first is <see cref="Position"/> long along the split (its content's,
/// until there's one) and keeps it as the split grows; the second takes the
/// rest. A pane's least width (or height) is as far as the divider goes.
/// </summary>
public sealed class Split : Node, IEnumerable<Node>
{
    public Split() : base(Native.Kind.Split) { }

    public void Add(Node child) => base.Add(child);

    public Orientation Orientation
    {
        set => Native.Check(Native.SetEnum(Id, Native.EnumProp.Orientation, (uint)value));
    }

    private double? _position;

    /// <summary>The first pane's length along the split, in points.</summary>
    public double? Position
    {
        get => _position;
        set
        {
            _position = value;
            if (value is { } position) Native.Check(Native.SetNumber(Id, Native.NumberProp.Number, position));
        }
    }

    /// <summary>The user moved the divider: the first pane's new length.</summary>
    public event Action<double>? Changed;

    private protected override void OnEvent(Native.Event e)
    {
        if (e.Kind != Native.EventKind.Changed || e.ValueKind != Native.ValueKind.Number) return;
        _position = e.Number;
        Changed?.Invoke(e.Number);
    }

    public IEnumerator<Node> GetEnumerator() => Children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>A button that stays pressed until it's pressed again, as the
/// platform makes one.</summary>
public sealed class ToggleButton : Node
{
    public ToggleButton(string label = "") : base(Native.Kind.ToggleButton) => Label = label;

    public string Label { set => Native.Check(Native.SetText(Id, Native.TextProp.Label, value)); }
    public ButtonStyle Style { set => Native.Check(Native.SetEnum(Id, Native.EnumProp.ButtonStyle, (uint)value)); }

    /// <summary>The app's own icon before its caption.</summary>
    public VectorIcon? Icon { set => Native.Check(Native.SetVectorIcon(Id, value)); }

    /// <summary>Shows only its icon; the caption stays its accessible
    /// name.</summary>
    public bool IconOnly { set => Native.Check(Native.SetBool(Id, Native.BoolProp.IconOnly, value)); }

    /// <summary>Its icon's colour, as for <see cref="Button.IconColor"/>.</summary>
    public Color IconColor { set => Native.Check(value.Set(Id)); }

    private bool _checked;

    public bool IsChecked
    {
        get => _checked;
        set
        {
            _checked = value;
            Native.Check(Native.SetBool(Id, Native.BoolProp.Checked, value));
        }
    }

    /// <summary>The user pressed it, or let it go.</summary>
    public event Action<bool>? Changed;

    private protected override unsafe void OnEvent(Native.Event e)
    {
        if (e.Kind != Native.EventKind.Changed || e.ValueKind != Native.ValueKind.Bool) return;
        _checked = e.Flag;
        Changed?.Invoke(e.Flag);
    }
}

public sealed class Checkbox : Node
{
    public Checkbox(string label = "") : base(Native.Kind.Checkbox) => Label = label;

    public string Label { set => Native.Check(Native.SetText(Id, Native.TextProp.Label, value)); }

    private bool _checked;

    public bool IsChecked
    {
        get => _checked;
        set
        {
            _checked = value;
            Native.Check(Native.SetBool(Id, Native.BoolProp.Checked, value));
        }
    }

    /// <summary>The user checked or unchecked it.</summary>
    public event Action<bool>? Changed;

    private protected override unsafe void OnEvent(Native.Event e)
    {
        if (e.Kind != Native.EventKind.Changed || e.ValueKind != Native.ValueKind.Bool) return;
        // The native checkbox already shows it: only remember it.
        _checked = e.Flag;
        Changed?.Invoke(e.Flag);
    }
}

/// <summary>What text fields share: their text, placeholder and name.</summary>
public abstract class TextField : Node
{
    private string _value = "";
    /// <summary>Where its value goes, when not straight to the node (an
    /// extras widget's area, whose hint follows it).</summary>
    private readonly Action<string>? _send;

    private protected TextField(Native.Kind kind) : base(kind) { }

    private protected TextField(uint id, Action<string> send) : base(id)
    {
        _send = send;
    }

    public string Placeholder { set => Native.Check(Native.SetText(Id, Native.TextProp.Placeholder, value)); }

    /// <summary>What's wrong with its text, or null: the platform marks the
    /// field where it has a way to (GNOME's red entries), and assistive
    /// technology reads it as invalid, with this message. Show the message
    /// by the field too. (A text field, password field or text area.)</summary>
    public string? Error { set => Native.Check(Native.SetText(Id, Native.TextProp.Error, value ?? "")); }

    /// <summary>The name assistive technology reads, when no label is next
    /// to it.</summary>
    public string AccessibleName { set => Native.Check(Native.SetText(Id, Native.TextProp.A11yLabel, value)); }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            if (_send is not null) _send(value);
            else Native.Check(Native.SetText(Id, Native.TextProp.Value, value));
        }
    }

    /// <summary>The user changed the text.</summary>
    public event Action<string>? Changed;

    private protected override unsafe void OnEvent(Native.Event e)
    {
        if (e.Kind != Native.EventKind.Changed || e.ValueKind != Native.ValueKind.Text) return;
        // The native field already shows it: only remember it.
        _value = Native.Utf8(e.Text, e.TextLen);
        Changed?.Invoke(_value);
    }
}

/// <summary>The platform's text view for several lines: Return starts a
/// new one.</summary>
public sealed class TextArea : TextField
{
    public TextArea() : base(Native.Kind.TextArea) { }

    internal TextArea(uint id, Action<string> send) : base(id, send) { }

    /// <summary>Its text in one of the platform's text styles: monospace
    /// for code or logs.</summary>
    public TextStyle Style { set => Native.Check(Native.SetEnum(Id, Native.EnumProp.TextStyle, (uint)value)); }

    /// <summary>Shows its text, which can be selected and copied, but not
    /// edited.</summary>
    public bool ReadOnly { set => Native.Check(Native.SetBool(Id, Native.BoolProp.ReadOnly, value)); }

    /// <summary>Its lines wrap to its width, as they do unless set false;
    /// without, they scroll sideways.</summary>
    public bool LineWrap { set => Native.Check(Native.SetBool(Id, Native.BoolProp.LineWrap, value)); }

    /// <summary>Keeps its end in view as its text is set, while it's there:
    /// a log that grows. Scrolled up, it stays put.</summary>
    public bool FollowsEnd { set => Native.Check(Native.SetBool(Id, Native.BoolProp.FollowsEnd, value)); }
}

public sealed class TextInput : TextField
{
    public TextInput() : base(Native.Kind.TextInput) { }

    /// <summary>Shows its text, which can be selected and copied, but not
    /// edited.</summary>
    public bool ReadOnly { set => Native.Check(Native.SetBool(Id, Native.BoolProp.ReadOnly, value)); }

    /// <summary>The user pressed Return.</summary>
    public event Action? Submit;

    private protected override void OnEvent(Native.Event e)
    {
        base.OnEvent(e);
        if (e.Kind == Native.EventKind.Submit) Submit?.Invoke();
    }
}

/// <summary>The platform's password field: it hides what's typed, as
/// the platform's own does.</summary>
public sealed class PasswordInput : TextField
{
    public PasswordInput() : base(Native.Kind.PasswordInput) { }

    /// <summary>The user pressed Return.</summary>
    public event Action? Submit;

    private protected override void OnEvent(Native.Event e)
    {
        base.OnEvent(e);
        if (e.Kind == Native.EventKind.Submit) Submit?.Invoke();
    }
}

/// <summary>The platform's search field. It searches when the platform
/// does: after a pause in typing, on Return, when cleared.</summary>
public sealed class SearchInput : TextField
{
    public SearchInput() : base(Native.Kind.SearchInput) { }

    public event Action<string>? Search;

    /// <summary>Searches on Return (and when cleared), not as the user
    /// types: for searches that take a while, with each platform's own
    /// setting for it.</summary>
    public bool SearchOnReturn { set => Native.Check(Native.SetBool(Id, Native.BoolProp.SearchOnReturn, value)); }

    /// <summary>What it suggests for its text, in a list under it as the
    /// platform shows a field's suggestions: a title and an optional second
    /// line each. The field keeps the keyboard: the arrows move through the
    /// list, Return or a click picks (<see cref="SuggestionChosen"/>),
    /// Escape hides it. Empty: none.</summary>
    public IReadOnlyList<(string Title, string? Detail)> Suggestions
    {
        set => Native.Check(Native.SetSuggestions(Id, value));
    }

    /// <summary>The user picked a suggestion, by its index. The text is the
    /// app's to set.</summary>
    public event Action<int>? SuggestionChosen;

    private protected override unsafe void OnEvent(Native.Event e)
    {
        base.OnEvent(e);
        if (e.Kind == Native.EventKind.Search) Search?.Invoke(Native.Utf8(e.Text, e.TextLen));
        else if (e.Kind == Native.EventKind.SuggestionChosen) SuggestionChosen?.Invoke((int)e.Index);
    }
}

public enum DocumentTabsStyle : uint { Automatic, Strip, Window, TitleRow }

/// <summary>
/// Tabs for the documents an app has open, as a browser's: titles only, the
/// app shows the chosen document itself. Where they show is the platform's
/// (<see cref="DocumentTabsStyle.Automatic"/>): the title bar on macOS and
/// Windows, a strip on GNOME and KDE.
/// </summary>
public sealed class DocumentTabs : Node
{
    public DocumentTabs() : base(Native.Kind.DocumentTabs) { }

    public IReadOnlyList<string> Titles
    {
        set => Native.Check(Native.SetStrings(Id, Native.StringsProp.TabTitles, value));
    }

    /// <summary>The chosen tab; -1 for none.</summary>
    public int SelectedIndex { set => Native.Check(Native.SetIndex(Id, Native.IndexProp.SelectedIndex, value)); }

    public bool Closable { set => Native.Check(Native.SetBool(Id, Native.BoolProp.TabsClosable, value)); }
    public bool Reorderable { set => Native.Check(Native.SetBool(Id, Native.BoolProp.TabsReorderable, value)); }
    public bool NewTabButton { set => Native.Check(Native.SetBool(Id, Native.BoolProp.NewTabButton, value)); }

    public DocumentTabsStyle Style
    {
        set => Native.Check(Native.SetEnum(Id, Native.EnumProp.DocumentTabsStyle, (uint)value));
    }

    /// <summary>The user chose a tab: its index.</summary>
    public event Action<int>? Chosen;

    /// <summary>The user asked to close a tab. It stays until the app
    /// takes it away (new titles).</summary>
    public event Action<int>? CloseRequested;

    /// <summary>The user moved a tab, which shows there already: from, to.</summary>
    public event Action<int, int>? Moved;

    public event Action? NewTabRequested;

    private protected override void OnEvent(Native.Event e)
    {
        switch (e.Kind)
        {
            case Native.EventKind.Changed when e.ValueKind == Native.ValueKind.Index:
                Chosen?.Invoke((int)e.Index);
                break;
            case Native.EventKind.TabCloseRequested:
                CloseRequested?.Invoke((int)e.Index);
                break;
            case Native.EventKind.TabMoved:
                Moved?.Invoke((int)e.Index, (int)e.To);
                break;
            case Native.EventKind.NewTabRequested:
                NewTabRequested?.Invoke();
                break;
        }
    }
}

/// <summary>The platform's drop-down. One option is always chosen, as GTK's
/// can't show none.</summary>
/// <summary>The platform's slider: a number in a range, which snaps to its
/// step where the platform's sliders snap.</summary>
public sealed class Slider : Node
{
    public Slider(string label = "") : base(Native.Kind.Slider) => Label = label;

    /// <summary>Its accessible name.</summary>
    public string Label { set => Native.Check(Native.SetText(Id, Native.TextProp.Label, value)); }

    /// <summary>Its range, and its step (0: the platform's). Set it before
    /// its value.</summary>
    public void SetRange(double min, double max, double step = 0) => Native.Check(Native.SetRange(Id, min, max, step));

    public double Value { set => Native.Check(Native.SetNumber(Id, Native.NumberProp.Number, value)); }

    /// <summary>The user moved it: its value.</summary>
    public event Action<double>? Changed;

    private protected override void OnEvent(Native.Event e)
    {
        if (e.Kind == Native.EventKind.Changed && e.ValueKind == Native.ValueKind.Number) Changed?.Invoke(e.Number);
    }
}

/// <summary>The platform's field for a whole number, with buttons that step
/// it.</summary>
public sealed class NumberInput : Node
{
    public NumberInput(string label = "") : base(Native.Kind.NumberInput) => Label = label;

    /// <summary>Its accessible name.</summary>
    public string Label { set => Native.Check(Native.SetText(Id, Native.TextProp.Label, value)); }

    /// <summary>Its range, and what its buttons add (0: 1). Set it before
    /// its value.</summary>
    public void SetRange(int min, int max, int step = 0) => Native.Check(Native.SetRange(Id, min, max, step));

    public int Value { set => Native.Check(Native.SetNumber(Id, Native.NumberProp.Number, value)); }

    /// <summary>The user changed it: its value.</summary>
    public event Action<int>? Changed;

    private protected override void OnEvent(Native.Event e)
    {
        if (e.Kind == Native.EventKind.Changed && e.ValueKind == Native.ValueKind.Number) Changed?.Invoke((int)e.Number);
    }
}

public sealed class Select : Node
{
    public Select(string label = "") : base(Native.Kind.Select) => Label = label;

    /// <summary>Its accessible name.</summary>
    public string Label { set => Native.Check(Native.SetText(Id, Native.TextProp.Label, value)); }

    public IReadOnlyList<string> Options
    {
        set => Native.Check(Native.SetStrings(Id, Native.StringsProp.Options, value));
    }

    public int SelectedIndex { set => Native.Check(Native.SetIndex(Id, Native.IndexProp.SelectedIndex, value)); }

    /// <summary>The user chose an option: its index.</summary>
    public event Action<int>? Changed;

    private protected override void OnEvent(Native.Event e)
    {
        if (e.Kind == Native.EventKind.Changed && e.ValueKind == Native.ValueKind.Index) Changed?.Invoke((int)e.Index);
    }
}

/// <summary>
/// The platform's radio buttons, one per option, down a column. None is
/// chosen until the app or the user picks one.
/// </summary>
public sealed class RadioGroup : Node
{
    public RadioGroup(string label = "") : base(Native.Kind.RadioGroup) => Label = label;

    /// <summary>Its accessible name.</summary>
    public string Label { set => Native.Check(Native.SetText(Id, Native.TextProp.Label, value)); }

    public IReadOnlyList<string> Options
    {
        set => Native.Check(Native.SetStrings(Id, Native.StringsProp.Options, value));
    }

    /// <summary>The chosen option; -1 for none.</summary>
    public int SelectedIndex { set => Native.Check(Native.SetIndex(Id, Native.IndexProp.SelectedIndex, value)); }

    /// <summary>The user chose an option: its index.</summary>
    public event Action<int>? Changed;

    private protected override void OnEvent(Native.Event e)
    {
        if (e.Kind == Native.EventKind.Changed && e.ValueKind == Native.ValueKind.Index) Changed?.Invoke((int)e.Index);
    }
}

public enum ScrollAxes : uint { Vertical, Horizontal, Both }

/// <summary>
/// The platform's scroll view. What it scrolls goes in its
/// <see cref="Content"/>, which keeps its natural length along the
/// scrolling axes and stretches across the other: a column, unless it
/// scrolls only sideways (a row).
/// </summary>
public sealed unsafe class ScrollView : Node
{
    public ScrollView(ScrollAxes axes = ScrollAxes.Vertical) : base(Create(axes, out var content))
    {
        Content = new Container(content);
        Adopt(Content);
    }

    private static uint Create(ScrollAxes axes, out uint content)
    {
        uint inner = 0;
        var outer = Native.CheckNode(Native.CreateScrollView((uint)axes, &inner));
        content = inner;
        return outer;
    }

    /// <summary>What it scrolls.</summary>
    public Container Content { get; }

    /// <summary>Scrolls to this offset of its content, in points, clamped
    /// to the content as laid out (past the end is the end): once layout
    /// has run.</summary>
    public void ScrollTo(float x, float y) => Native.Check(Native.ScrollTo(Id, x, y));
}

/// <summary>How tabs show where the platform has more than one way (GNOME
/// and KDE): navigation tabs by default, or a tab bar over the pages.</summary>
public enum TabsStyle : uint { Automatic, TabBar, Navigation }

/// <summary>
/// The platform's tab view: pages, one shown at a time, and a strip of
/// their titles to pick one. Every page stays built.
/// </summary>
public sealed class Tabs : Node
{
    private readonly List<string> _titles = [];

    public Tabs() : base(Native.Kind.Tabs) { }

    /// <summary>Adds a page, after the others, with its tab's title.</summary>
    public void Add(string title, Node page)
    {
        _titles.Add(title);
        Native.Check(Native.SetStrings(Id, Native.StringsProp.TabTitles, _titles));
        base.Add(page);
    }

    public TabsStyle Style { set => Native.Check(Native.SetEnum(Id, Native.EnumProp.TabsStyle, (uint)value)); }

    /// <summary>The page shown; not reported back as <see cref="Changed"/>.</summary>
    public int SelectedIndex
    {
        get => _selected;
        set
        {
            _selected = value;
            Native.Check(Native.SetIndex(Id, Native.IndexProp.SelectedIndex, value));
        }
    }

    private int _selected;

    /// <summary>The user picked a tab: its index.</summary>
    public event Action<int>? Changed;

    private protected override void OnEvent(Native.Event e)
    {
        if (e.Kind != Native.EventKind.Changed || e.ValueKind != Native.ValueKind.Index) return;
        _selected = (int)e.Index;
        Changed?.Invoke(_selected);
    }
}

/// <summary>The platform's busy indicator, for work of unknown length.</summary>
public sealed class Spinner : Node
{
    public Spinner() : base(Native.Kind.Spinner) { }
}
