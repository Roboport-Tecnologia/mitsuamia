namespace Mitsuamia;

/// <summary>An entry of a menu: an item, a separator or a submenu.</summary>
public abstract record MenuEntry;

/// <summary>
/// An item that runs <see cref="Action"/> when chosen. <see cref="Checked"/>
/// shows a check mark (or a radio dot, with <see cref="Radio"/>); null, none.
/// </summary>
public sealed record MenuItem(string Title, Action Action) : MenuEntry
{
    public bool Enabled { get; init; } = true;
    public bool? Checked { get; init; }
    public bool Radio { get; init; }
}

public sealed record MenuSeparator : MenuEntry
{
    public static MenuSeparator Instance { get; } = new();
}

public sealed record Submenu(string Title, IReadOnlyList<MenuEntry> Entries) : MenuEntry;
