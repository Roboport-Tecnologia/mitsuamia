using System.Collections;
using System.Windows.Input;

namespace Mitsuamia;

public enum TextStyle : uint { LargeTitle, Title, Headline, Body, Callout, Caption, Monospace }

public enum ButtonRole : uint { Normal, Default, Cancel, Destructive }

public enum ButtonStyle : uint { Automatic, Bordered, Borderless }

public enum HeaderEdge : uint { Automatic, Soft, Hard }

/// <summary>A flexbox container, a column by default. Children go in with a
/// collection initializer: <c>new Column { a, b }</c>.</summary>
public class Container : Node, IEnumerable<Node>
{
    public Container() : base(Native.Kind.Container) { }

    public void Add(Node child) => base.Add(child);
    public void Insert(int index, Node child) => base.Add(child, index);

    public float Gap { set => Native.Check(Native.SetGap(Id, value)); }

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

    /// <summary>How a header meets what scrolls under it, on macOS 26.1 and
    /// later.</summary>
    public HeaderEdge HeaderEdge { set => Native.Check(Native.SetEnum(Id, Native.EnumProp.HeaderEdge, (uint)value)); }

    public IEnumerator<Node> GetEnumerator() => Children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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
}

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

    private protected TextField(Native.Kind kind) : base(kind) { }

    public string Placeholder { set => Native.Check(Native.SetText(Id, Native.TextProp.Placeholder, value)); }

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
            Native.Check(Native.SetText(Id, Native.TextProp.Value, value));
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

public sealed class TextInput : TextField
{
    public TextInput() : base(Native.Kind.TextInput) { }

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

    private protected override unsafe void OnEvent(Native.Event e)
    {
        base.OnEvent(e);
        if (e.Kind == Native.EventKind.Search) Search?.Invoke(Native.Utf8(e.Text, e.TextLen));
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

/// <summary>The platform's busy indicator, for work of unknown length.</summary>
public sealed class Spinner : Node
{
    public Spinner() : base(Native.Kind.Spinner) { }
}
