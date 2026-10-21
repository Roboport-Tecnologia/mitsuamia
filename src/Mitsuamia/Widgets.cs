using System.Collections;
using System.Windows.Input;

namespace Mitsuamia;

public enum TextStyle : uint { LargeTitle, Title, Headline, Body, Callout, Caption, Monospace }

public enum ButtonRole : uint { Normal, Default, Cancel, Destructive }

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

public sealed class Window : Node
{
    public Window(string title, float width, float height)
        : base(Native.CheckNode(CreateWindow(title, width, height))) { }

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
    public bool IsEnabled { set => Native.Check(Native.SetBool(Id, Native.BoolProp.Enabled, value)); }

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

public sealed class TextInput : Node
{
    private string _value = "";

    public TextInput() : base(Native.Kind.TextInput) { }

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

    /// <summary>The user pressed Return.</summary>
    public event Action? Submit;

    private protected override unsafe void OnEvent(Native.Event e)
    {
        switch (e.Kind)
        {
            case Native.EventKind.Changed when e.ValueKind == Native.ValueKind.Text:
                // The native field already shows it: only remember it.
                _value = Native.Utf8(e.Text, e.TextLen);
                Changed?.Invoke(_value);
                break;
            case Native.EventKind.Submit:
                Submit?.Invoke();
                break;
        }
    }
}
