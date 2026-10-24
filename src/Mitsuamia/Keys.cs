namespace Mitsuamia;

/// <summary>A key on its own: <see cref="Char"/> is any character's.</summary>
public enum Key : uint
{
    Char, Enter, Escape, Tab, Backspace, Delete, Up, Down, Left, Right, Home, End, PageUp, PageDown, F,
}

/// <summary>
/// A key with its modifiers. <see cref="Primary"/> is ⌘ on macOS and Ctrl
/// elsewhere. A letter is lower case: Shift is <see cref="Shift"/>.
/// </summary>
public readonly record struct Shortcut(Key Key, uint Code = 0, bool Primary = false, bool Shift = false, bool Alt = false)
{
    /// <summary>A character's key: <c>Shortcut.Of('p')</c>.</summary>
    public static Shortcut Of(char c) => new(Key.Char, char.ToLowerInvariant(c));

    public static Shortcut Of(Key key) => new(key);

    /// <summary>A function key, from F1.</summary>
    public static Shortcut Function(int number) => new(Key.F, (uint)number);

    public Shortcut WithPrimary() => this with { Primary = true };
    public Shortcut WithShift() => this with { Shift = true };
    public Shortcut WithAlt() => this with { Alt = true };
}
