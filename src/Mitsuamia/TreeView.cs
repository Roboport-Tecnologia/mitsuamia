using System.Collections.Specialized;
using System.Runtime.InteropServices;

namespace Mitsuamia;

/// <summary>
/// The platform's own tree control: rows that nest, opened and closed by a
/// disclosure the platform draws, indented its way. It builds rows only as
/// it shows them, and shows an item's children while it's open. Its
/// selection, activation and scrolling are a list's.
/// </summary>
/// <remarks>
/// <paramref name="children"/> gives an item's children, or null for an
/// item that never opens (a file); an empty list opens to nothing (a folder
/// not read yet). The tree reads them again when <see cref="Roots"/> changes
/// or on <see cref="Refresh"/>.
/// </remarks>
public unsafe class TreeView<T> : ListView where T : class
{
    private readonly Func<T, string> key;
    private readonly Func<T, IReadOnlyList<T>?> children;
    private readonly Func<T, Node> row;

    private readonly Dictionary<string, T> _byKey = [];
    // Each opening item's children's keys, and the top ones', to count the
    // rows that show.
    private readonly Dictionary<string, List<string>> _children = [];
    private readonly List<string> _top = [];
    private IReadOnlyList<T> _roots = [];
    private IReadOnlyList<T> _selected = [];
    private IReadOnlyList<T> _expanded = [];

    /// <param name="moves">Where rows dragged within the tree can go, as a
    /// list's: onto a row is into it, between rows at that place among its
    /// parent's rows, and an item never goes inside itself.
    /// <see cref="Moved"/> says where they went; the app moves its
    /// items.</param>
    /// <param name="itemAnimations">False: rows don't animate in where the
    /// platform animates them (WinUI's), as a list's.</param>
    public TreeView(Func<T, string> key, Func<T, IReadOnlyList<T>?> children, Func<T, Node> row,
        SelectionMode mode = SelectionMode.Single, RowMoves moves = RowMoves.None, bool itemAnimations = true)
        : base(box => CreateTree(box, moves, itemAnimations), mode)
    {
        this.key = key;
        this.children = children;
        this.row = row;
        // The roots usually outlive the tree: stop following them with it.
        Forgotten += () =>
        {
            if (_roots is INotifyCollectionChanged roots) roots.CollectionChanged -= OnRootsChanged;
        };
    }

    private static uint CreateTree(Box box, RowMoves moves, bool itemAnimations)
    {
        var callbacks = new Native.TreeCallbacks
        {
            BuildRow = &BuildRow,
            ReleaseRow = &ReleaseRow,
            SelectionChanged = &SelectionChangedCallback,
            Activated = &ActivatedCallback,
            ExpandedChanged = &ExpandedChangedCallback,
            // Lives as long as the app: trees are few.
            User = GCHandle.ToIntPtr(GCHandle.Alloc(box)),
            Still = itemAnimations ? 0u : 1u,
            Moves = (uint)moves,
            Moved = &MovedCallback,
        };
        return Native.CheckNode(Native.CreateTree(callbacks));
    }

    /// <summary>The top items. An observable collection is followed.</summary>
    public IReadOnlyList<T> Roots
    {
        get => _roots;
        set
        {
            if (_roots is INotifyCollectionChanged old) old.CollectionChanged -= OnRootsChanged;
            _roots = value;
            if (value is INotifyCollectionChanged now) now.CollectionChanged += OnRootsChanged;
            Refresh();
        }
    }

    /// <summary>Reads the items and their children again, after they
    /// changed below the top.</summary>
    public void Refresh()
    {
        _byKey.Clear();
        _children.Clear();
        _top.Clear();
        var nodes = new List<(string Key, string Parent, bool Opens)>();
        void Add(IReadOnlyList<T> items, string parent, List<string> siblings)
        {
            foreach (var item in items)
            {
                var k = key(item);
                // A key seen already, above or elsewhere, is left out: its
                // row would loop or show twice.
                if (!_byKey.TryAdd(k, item)) continue;
                siblings.Add(k);
                var kids = children(item);
                nodes.Add((k, parent, kids is not null));
                if (kids is null) continue;
                _children[k] = [];
                Add(kids, k, _children[k]);
            }
        }
        Add(_roots, "", _top);
        Native.Check(Native.TreeSetNodes(Id, nodes));
        CountShown();
    }

    /// <summary>The rows that show: the top ones, and the children of open
    /// ones, all the way down.</summary>
    private void CountShown()
    {
        var open = _expanded.Select(key).ToHashSet();
        var count = 0;
        void Walk(List<string> keys)
        {
            foreach (var k in keys)
            {
                count++;
                if (open.Contains(k) && _children.TryGetValue(k, out var kids)) Walk(kids);
            }
        }
        Walk(_top);
        SetItemCount(count);
    }

    private void OnRootsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    /// <summary>The open items: the user opens and closes them, and so can
    /// the app. One in a closed item stays open, and shows open when that
    /// one opens.</summary>
    public IReadOnlyList<T> ExpandedItems
    {
        get => _expanded;
        set
        {
            _expanded = value;
            Native.Check(Native.TreeSetExpanded(Id, value.Select(key).ToArray()));
            CountShown();
        }
    }

    public IReadOnlyList<T> SelectedItems
    {
        get => _selected;
        set
        {
            _selected = value;
            Native.Check(Native.ListSetSelected(Id, value.Select(key).ToArray()));
        }
    }

    public T? SelectedItem
    {
        get => _selected.Count > 0 ? _selected[0] : null;
        set => SelectedItems = value is null ? [] : [value];
    }

    /// <summary>Scrolls just enough to show the item's row, once its
    /// parents are open.</summary>
    public void ScrollTo(T item) => Native.Check(Native.ListScrollTo(Id, key(item)));

    /// <summary>The user opened or closed items: the open ones.</summary>
    public event Action<IReadOnlyList<T>>? ExpandedChanged;

    /// <summary>The user changed the selection.</summary>
    public event Action<IReadOnlyList<T>>? SelectionChanged;

    /// <summary>The user activated a row: double-click or Return. It opens
    /// nothing.</summary>
    public event Action<T>? Activated;

    private protected override Node? Row(string k) => _byKey.TryGetValue(k, out var item) ? row(item) : null;

    private protected override void OnSelectionChanged(IReadOnlyList<string> keys)
    {
        _selected = keys.Select(k => _byKey.GetValueOrDefault(k)).OfType<T>().ToArray();
        SelectionChanged?.Invoke(_selected);
    }

    private protected override void OnActivated(string k)
    {
        if (_byKey.TryGetValue(k, out var item)) Activated?.Invoke(item);
    }

    /// <summary>The user dragged rows within the tree and dropped them: the
    /// items, in the tree's order, and where (onto an item is into
    /// it).</summary>
    public event Action<IReadOnlyList<T>, RowDrop<T>>? Moved;

    private protected override void OnMoved(IReadOnlyList<string> keys, RowDropKind kind, string k)
    {
        var items = keys.Select(key => _byKey.GetValueOrDefault(key)).OfType<T>().ToArray();
        var target = kind == RowDropKind.End ? null : _byKey.GetValueOrDefault(k);
        if (items.Length > 0 && (kind == RowDropKind.End || target != null))
            Moved?.Invoke(items, new RowDrop<T>(kind, target));
    }

    private protected override void OnExpandedChanged(IReadOnlyList<string> keys)
    {
        _expanded = keys.Select(k => _byKey.GetValueOrDefault(k)).OfType<T>().ToArray();
        CountShown();
        ExpandedChanged?.Invoke(_expanded);
    }
}
