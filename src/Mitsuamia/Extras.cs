namespace Mitsuamia;

// mitsuami-extras' widgets, for what some platforms have no native control
// for. They come from extras' C ABI, in one library with mitsuami's:
// MITSUAMI_FFI names mitsuami-extras-ffi's library.

/// <summary>
/// A text area with a placeholder on every platform: the platform's own
/// where its text area has one (Qt, WinUI), elsewhere a hint over the
/// first line that clicks pass through (AppKit, GTK). This node is a column
/// the text area fills: size and place it. The text area is
/// <see cref="Area"/>, for its value, events and other props.
/// </summary>
public sealed class TextAreaWithPlaceholder : Node
{
    public TextAreaWithPlaceholder(string placeholder = "") : this(Native.ExtrasCreateTextArea(placeholder)) { }

    private TextAreaWithPlaceholder((uint Column, uint Area) ids) : base(ids.Column)
    {
        var column = ids.Column;
        // Its value goes through extras, so the hint follows it.
        Area = new TextArea(ids.Area, v => Native.Check(Native.ExtrasTextAreaSetValue(column, v)));
        Forgotten += Area.Forget;
    }

    public TextArea Area { get; }

    public string Placeholder { set => Native.Check(Native.ExtrasTextAreaSetPlaceholder(Id, value)); }
}
