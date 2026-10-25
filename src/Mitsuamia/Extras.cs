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

/// <summary>
/// A field with its error message under it, shown while it has one, as
/// every platform's apps show theirs; the field is marked too, the
/// platform's way (GNOME's red entry), for assistive technology as well.
/// This node is a column holding <paramref name="content"/> (the field, or
/// a row with it and its buttons), then the message: size and place it.
/// </summary>
public sealed class FieldWithError : Node
{
    public FieldWithError(Node content, TextField field)
        : base(Native.CheckNode(Native.ExtrasCreateFieldWithError(content.Id, field.Id)))
    {
        // The column holds it: destroyed with it.
        Forgotten += content.Forget;
    }

    /// <summary>What's wrong with the field, or null.</summary>
    public string? Error { set => Native.Check(Native.ExtrasFieldSetError(Id, value ?? "")); }
}
