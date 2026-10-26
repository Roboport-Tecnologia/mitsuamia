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
    /// <summary>Shown by it; in the menu bar it also chooses it.</summary>
    public Shortcut? Shortcut { get; init; }
    /// <summary>An item every app has, which a platform may move to a place
    /// of its own (macOS's app menu). Menu bars only.</summary>
    public MenuRole Role { get; init; }
}

public enum MenuRole : uint { None, About, Settings, Quit }

/// <summary>A menu entry as the C ABI takes it.</summary>
internal readonly record struct FlatMenuEntry(uint Kind, uint Id, string Title, bool Enabled, uint Check, MenuRole Role = MenuRole.None, Shortcut? Shortcut = null);

/// <summary>
/// The app's menu bar: the platform's (macOS's, with the roles' items in
/// the app menu), or however it shows an app's menus; or a window's own
/// menus, shown with the app's, and only in that window where each window
/// has its menus (Windows, GNOME, KDE). Set it again to change it.
/// </summary>
public static unsafe class MenuBar
{
    // By window: 0 is the app's.
    private static readonly Dictionary<uint, Dictionary<uint, Action>> s_actions = [];

    public static void Set(IReadOnlyList<Submenu> menus) => Set(0, menus);

    public static void Set(Window window, IReadOnlyList<Submenu> menus) => Set(window.Id, menus);

    private static void Set(uint window, IReadOnlyList<Submenu> menus)
    {
        var (flat, actions) = Node.FlattenMenu(menus);
        s_actions[window] = actions;
        Native.Check(Native.SetMenuBar(window, flat, &Chosen));
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void Chosen(nint user, uint id)
    {
        if (s_actions.GetValueOrDefault((uint)user)?.GetValueOrDefault(id) is { } action)
            Application.Guard(action);
    }
}

public sealed record MenuSeparator : MenuEntry
{
    public static MenuSeparator Instance { get; } = new();
}

public sealed record Submenu(string Title, IReadOnlyList<MenuEntry> Entries) : MenuEntry;
