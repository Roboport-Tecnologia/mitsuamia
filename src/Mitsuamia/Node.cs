using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mitsuamia;

public enum Align : uint { Unset, Start, End, Center, Stretch, Baseline }

public enum Justify : uint { Unset, Start, End, Center, Stretch, SpaceBetween, SpaceAround, SpaceEvenly }

/// <summary>
/// A node of mitsuami's tree. Nodes stay alive while they're shown: the
/// registry holds them until they're destroyed, since mitsuami holds only
/// their ids.
/// </summary>
public abstract unsafe class Node
{
    private static readonly Dictionary<uint, Node> Live = [];
    private readonly List<Node> _children = [];
    private GCHandle _self;

    public uint Id { get; }
    public Node? Parent { get; private set; }
    public IReadOnlyList<Node> Children => _children;

    private protected Node(uint id)
    {
        Application.VerifyAccess();
        Id = id;
        Live[id] = this;
        _self = GCHandle.Alloc(this, GCHandleType.Weak);
        Native.Check(Native.OnEvent(id, &Dispatch, GCHandle.ToIntPtr(_self)));
    }

    private protected Node(Native.Kind kind) : this(Native.CheckNode(Native.Create(kind))) { }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Dispatch(nint user, uint id, Native.Event* e)
    {
        if (GCHandle.FromIntPtr(user).Target is not Node node) return;
        var ev = *e;
        Application.Guard(() => node.OnCommonEvent(ev));
    }

    private protected virtual void OnEvent(Native.Event e) { }

    // What every node takes: its context menu's items.
    private Dictionary<uint, Action>? _menuActions;

    /// <summary>Its context menu, shown on the platform's own gestures
    /// (right-click, Control-click, long press, the Menu key); empty: none.
    /// A child without one shows its container's.</summary>
    public IReadOnlyList<MenuEntry> ContextMenu
    {
        set
        {
            var (flat, actions) = FlattenMenu(value);
            Native.Check(Native.SetContextMenu(Id, flat));
            _menuActions = actions;
        }
    }

    /// <summary>A weak handle to it, for the C ABI's callbacks.</summary>
    private protected nint Handle => GCHandle.ToIntPtr(_self);

    private Func<IReadOnlyList<MenuEntry>>? _menuOnOpen;

    /// <summary>Builds its context menu each time the platform opens it,
    /// for a menu that depends on what's selected or under the pointer
    /// then (a table's rows); it reads what it needs then. Null: the menu
    /// is <see cref="ContextMenu"/>'s.</summary>
    public Func<IReadOnlyList<MenuEntry>>? ContextMenuOnOpen
    {
        set
        {
            _menuOnOpen = value;
            Native.Check(value is null
                ? Native.SetContextMenuOnOpen(Id, null, 0)
                : Native.SetContextMenuOnOpen(Id, &BuildMenu, Handle));
        }
    }

    /// <summary>The platform is opening a menu built as it opens: the
    /// node's function builds it, and it's set as its context menu.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void BuildMenu(nint user, uint id)
    {
        if (GCHandle.FromIntPtr(user).Target is not Node { _menuOnOpen: { } build } node) return;
        Application.Guard(() => node.ContextMenu = build());
    }

    /// <summary>A menu's entries in the C ABI's order, submenus between
    /// their start and end, and its items' actions by the ids given them.</summary>
    internal static (List<FlatMenuEntry>, Dictionary<uint, Action>) FlattenMenu(IEnumerable<MenuEntry> menu)
    {
        var flat = new List<FlatMenuEntry>();
        var actions = new Dictionary<uint, Action>();
        void Flatten(IEnumerable<MenuEntry> entries)
        {
            foreach (var entry in entries)
            {
                switch (entry)
                {
                    case MenuItem item:
                        var id = (uint)actions.Count + 1;
                        actions[id] = item.Action;
                        uint check = item.Checked switch
                        {
                            null => 0,
                            bool on when item.Radio => on ? 4u : 3u,
                            bool on => on ? 2u : 1u,
                        };
                        flat.Add(new(0, id, item.Title, item.Enabled, check, item.Role, item.Shortcut));
                        break;
                    case MenuSeparator:
                        flat.Add(new(1, 0, "", true, 0));
                        break;
                    case Submenu sub:
                        flat.Add(new(2, 0, sub.Title, true, 0));
                        Flatten(sub.Entries);
                        flat.Add(new(3, 0, "", true, 0));
                        break;
                }
            }
        }
        Flatten(menu);
        return (flat, actions);
    }

    // The keys it takes, and what each does.
    private List<(Shortcut Key, Action Action)>? _keys;

    /// <summary>
    /// Runs <paramref name="action"/> when <paramref name="key"/> is pressed
    /// while it (a container, list or table), or a control in it, has
    /// keyboard focus, and the focused control doesn't use the key itself.
    /// A key goes to the nearest node round the focused control that takes
    /// it. Nothing shows keys taken this way: give the command a menu item
    /// or a button too.
    /// </summary>
    public void OnKey(Shortcut key, Action action)
    {
        _keys ??= [];
        _keys.RemoveAll(k => k.Key == key);
        _keys.Add((key, action));
        Native.Check(Native.SetKeys(Id, _keys.Select(k => k.Key).ToArray()));
    }

    /// <summary>What tests find it by.</summary>
    public string TestId { set => Native.Check(Native.SetText(Id, Native.TextProp.TestId, value)); }

    private void OnCommonEvent(Native.Event e)
    {
        if (e.Kind == Native.EventKind.ContextMenuItem && _menuActions?.GetValueOrDefault((uint)e.Index) is { } action)
            action();
        else if (e.Kind == Native.EventKind.Key && _keys is { } keys && (int)e.Index < keys.Count)
            keys[(int)e.Index].Action();
        else
            OnEvent(e);
    }

    /// <summary>A child mitsuami put in it already: a scroll view's content.</summary>
    private protected void Adopt(Node child)
    {
        _children.Add(child);
        child.Parent = this;
    }

    private protected void Add(Node child, int? index = null)
    {
        Application.VerifyAccess();
        if (child.Parent is not null) throw new InvalidOperationException("mitsuamia: the node is already in the tree");
        if (index is { } i)
        {
            Native.Check(Native.InsertChild(Id, child.Id, (nuint)i));
            _children.Insert(i, child);
        }
        else
        {
            Native.Check(Native.AppendChild(Id, child.Id));
            _children.Add(child);
        }
        child.Parent = this;
    }

    /// <summary>Destroys the node and everything in it.</summary>
    public void Destroy()
    {
        Application.VerifyAccess();
        Parent?._children.Remove(this);
        Parent = null;
        Native.Check(Native.Destroy(Id));
        Forget();
    }

    /// <summary>mitsuami destroyed it (and its children) itself.</summary>
    internal void Forget()
    {
        foreach (var child in _children) child.Forget();
        _children.Clear();
        Live.Remove(Id);
        if (_self.IsAllocated) _self.Free();
        Forgotten?.Invoke();
        Forgotten = null;
    }

    /// <summary>The node is gone: destroyed, or a list row the platform
    /// let go. Bindings stop here.</summary>
    public event Action? Forgotten;

    // Layout, every node's. Null is auto.

    private Length? _width, _height, _minWidth, _minHeight, _maxWidth, _maxHeight;

    /// <summary>The least the layout gives it; null: none.</summary>
    public Length? MinWidth
    {
        set
        {
            _minWidth = value;
            Native.Check(Native.SetMinSize(Id, _minWidth ?? Length.Auto, _minHeight ?? Length.Auto));
        }
    }

    public Length? MinHeight
    {
        set
        {
            _minHeight = value;
            Native.Check(Native.SetMinSize(Id, _minWidth ?? Length.Auto, _minHeight ?? Length.Auto));
        }
    }

    /// <summary>The most the layout gives it; null: none.</summary>
    public Length? MaxWidth
    {
        set
        {
            _maxWidth = value;
            Native.Check(Native.SetMaxSize(Id, _maxWidth ?? Length.Auto, _maxHeight ?? Length.Auto));
        }
    }

    public Length? MaxHeight
    {
        set
        {
            _maxHeight = value;
            Native.Check(Native.SetMaxSize(Id, _maxWidth ?? Length.Auto, _maxHeight ?? Length.Auto));
        }
    }

    public Length? Width
    {
        set
        {
            _width = value;
            Native.Check(Native.SetSize(Id, _width ?? Length.Auto, _height ?? Length.Auto));
        }
    }

    public Length? Height
    {
        set
        {
            _height = value;
            Native.Check(Native.SetSize(Id, _width ?? Length.Auto, _height ?? Length.Auto));
        }
    }

    /// <summary>What the platform shows by the pointer when it rests on the
    /// control; empty for none.</summary>
    public string ToolTip { set => Native.Check(Native.SetText(Id, Native.TextProp.Tooltip, value)); }

    /// <summary>A disabled control can't be used, and shows it.</summary>
    public bool IsEnabled { set => Native.Check(Native.SetBool(Id, Native.BoolProp.Enabled, value)); }

    /// <summary>Hidden, it takes no room.</summary>
    public bool IsVisible { set => Native.Check(Native.SetHidden(Id, !value)); }

    public float Grow { set => Native.Check(Native.SetGrow(Id, value)); }

    /// <summary>Where its size starts before its container shares out the
    /// room left or takes back what's missing (CSS's <c>flex-basis</c>):
    /// auto, its own size, by default; 0 to share the room by
    /// <see cref="Grow"/> alone.</summary>
    public Length Basis { set => Native.Check(Native.SetBasis(Id, value)); }

    /// <summary>How much it gives up when its container's children don't
    /// fit, against the others (weighted by their sizes); 1 by default, 0
    /// never.</summary>
    public float Shrink { set => Native.Check(Native.SetShrink(Id, value)); }
    public Align AlignSelf { set => Native.Check(Native.SetAlignSelf(Id, (uint)value)); }

    /// <summary>Where it was last laid out, in points, relative to its
    /// native parent; zero until layout has run (read it in a posted
    /// callback after the change that made it).</summary>
    public unsafe (float X, float Y, float Width, float Height) Frame
    {
        get
        {
            var frame = stackalloc float[4];
            Native.Check(Native.Frame(Id, frame));
            return (frame[0], frame[1], frame[2], frame[3]);
        }
    }

    public Thickness Margin
    {
        set => Native.Check(Native.SetMargin(Id, value.Top, value.End, value.Bottom, value.Start));
    }

    /// <summary>Takes it out of its container's flow, placed by these
    /// insets from the container's edges (auto: not by that edge), over the
    /// nodes in it: a drawn backdrop behind a label. Null puts it back.</summary>
    public Thickness? Inset
    {
        set
        {
            var inset = value ?? new Thickness(Length.Auto);
            Native.Check(Native.SetPosition(Id, value.HasValue, inset.Top, inset.End, inset.Bottom, inset.Start));
        }
    }
}

/// <summary>Edges, with start and end following the text's direction.</summary>
public readonly record struct Thickness(Length Top, Length End, Length Bottom, Length Start)
{
    public Thickness(Length all) : this(all, all, all, all) { }
    public Thickness(Length vertical, Length horizontal) : this(vertical, horizontal, vertical, horizontal) { }
    public static implicit operator Thickness(float all) => new(all);
    public static implicit operator Thickness(Spacing all) => new(all);
}
