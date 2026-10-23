using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mitsuamia;

public enum SelectionMode : uint { None, Single, Multiple }

public enum ListStyle : uint { Automatic, Plain, Framed }

/// <summary>Where rows dragged within a list can be dropped: onto a row (into
/// a group), between rows (to reorder), or both.</summary>
[Flags]
public enum RowMoves : uint { None = 0, OntoRows = 1, BetweenRows = 2 }

/// <summary>Where rows dragged within a list were dropped: onto an item,
/// before one, or at the end (the list's empty part).</summary>
public readonly record struct RowDrop<T>(RowDropKind Kind, T? Item);

public enum RowDropKind : uint { On, Before, End }

/// <summary>
/// The platform's own list control. It builds rows only as it shows them,
/// so its items can be many; rows are told apart by their key.
/// </summary>
public abstract unsafe class ListView : Node
{
    // mitsuami knows the list before the node object exists, so the
    // callbacks find it through a box filled in once it does.
    private protected sealed class Box { public ListView? List; }

    private readonly Dictionary<uint, Node> _rows = [];

    private protected ListView(SelectionMode mode, RowMoves moves, bool itemAnimations)
        : this(box => CreateList(box, moves, itemAnimations), mode)
    {
    }

    /// <summary>A list made by <paramref name="create"/>: a table.</summary>
    private protected ListView(Func<Box, uint> create, SelectionMode mode) : base(Create(create, out var box))
    {
        box.List = this;
        SelectionMode = mode;
    }

    private static uint Create(Func<Box, uint> create, out Box box)
    {
        box = new Box();
        return create(box);
    }

    private static uint CreateList(Box box, RowMoves moves, bool itemAnimations)
    {
        var callbacks = new Native.ListCallbacks
        {
            BuildRow = &BuildRow,
            ReleaseRow = &ReleaseRow,
            SelectionChanged = &SelectionChangedCallback,
            Activated = &ActivatedCallback,
            // Lives as long as the app: lists are few.
            User = GCHandle.ToIntPtr(GCHandle.Alloc(box)),
            Moves = (uint)moves,
            Moved = &MovedCallback,
            Still = itemAnimations ? 0u : 1u,
        };
        return Native.CheckNode(Native.CreateList(callbacks));
    }

    private protected static ListView? Of(nint user) => ((Box)GCHandle.FromIntPtr(user).Target!).List;

    public SelectionMode SelectionMode
    {
        set => Native.Check(Native.SetEnum(Id, Native.EnumProp.SelectionMode, (uint)value));
    }

    public ListStyle Style { set => Native.Check(Native.SetEnum(Id, Native.EnumProp.ListStyle, (uint)value)); }

    /// <summary>A guess at rows' height, for rows not built yet.</summary>
    public float EstimatedRowHeight
    {
        set => Native.Check(Native.SetNumber(Id, Native.NumberProp.EstimatedRowHeight, value));
    }

    private protected abstract Node? Row(string key);
    private protected virtual Node? Cell(string key, int column) => null;
    private protected virtual void OnSorted(int column, bool descending) { }
    private protected abstract void OnSelectionChanged(IReadOnlyList<string> keys);
    private protected abstract void OnActivated(string key);
    private protected abstract void OnMoved(IReadOnlyList<string> keys, RowDropKind kind, string key);

    private protected void SetKeys(IReadOnlyList<string> keys)
    {
        Native.Check(Native.ListSetKeys(Id, keys));
        ItemCount = keys.Count;
        ItemsChanged?.Invoke();
    }

    /// <summary>How many items it has.</summary>
    public int ItemCount { get; private set; }

    /// <summary>Its items changed: for a list as tall as its rows.</summary>
    public event Action? ItemsChanged;
    private protected void SetSelectedKeys(IReadOnlyList<string> keys) => Native.Check(Native.ListSetSelected(Id, keys));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint BuildRow(nint user, uint list, byte* key, nuint len)
    {
        uint id = 0;
        Application.Guard(() =>
        {
            if (Of(user) is not { } view || view.Row(Native.Utf8(key, len)) is not { } row) return;
            view._rows[row.Id] = row;
            id = row.Id;
        });
        return id;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private protected static uint BuildCell(nint user, uint table, byte* key, nuint len, nuint column)
    {
        uint id = 0;
        Application.Guard(() =>
        {
            if (Of(user) is not { } view || view.Cell(Native.Utf8(key, len), (int)column) is not { } cell) return;
            view._rows[cell.Id] = cell;
            id = cell.Id;
        });
        return id;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private protected static void ReleaseRow(nint user, uint list, uint row)
    {
        Application.Guard(() =>
        {
            // mitsuami destroyed the row's nodes itself.
            if (Of(user) is { } view && view._rows.Remove(row, out var node)) node.Forget();
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private protected static void SelectionChangedCallback(nint user, uint list)
    {
        Application.Guard(() =>
        {
            if (Of(user) is not { } view) return;
            var count = Native.ListSelectedCount(list);
            var keys = new string[(int)count];
            for (nuint i = 0; i < count; i++) keys[i] = Native.ListSelectedKey(list, i);
            view.OnSelectionChanged(keys);
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void MovedCallback(nint user, uint list, Native.Str* keys, nuint count, uint to, byte* key, nuint len)
    {
        var moved = new string[(int)count];
        for (var i = 0; i < moved.Length; i++) moved[i] = Native.Utf8(keys[i].Ptr, keys[i].Len);
        var target = Native.Utf8(key, len);
        Application.Guard(() => Of(user)?.OnMoved(moved, (RowDropKind)to, target));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private protected static void SortedCallback(nint user, uint table, nuint column, byte descending)
    {
        Application.Guard(() => Of(user)?.OnSorted((int)column, descending != 0));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private protected static void ActivatedCallback(nint user, uint list, byte* key, nuint len)
    {
        var k = Native.Utf8(key, len);
        Application.Guard(() => Of(user)?.OnActivated(k));
    }
}

/// <summary>
/// A list of items, kept in step with an observable collection (an
/// <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>,
/// Avalonia's <c>AvaloniaList</c>). <paramref name="row"/> builds an item's
/// row when the platform shows it, as a <c>DataTemplate</c> does.
/// </summary>
public class ListView<T> : ListView where T : class
{
    private readonly Func<T, string> key;
    private readonly Func<T, Node> row;

    /// <param name="moves">Where rows dragged within the list can go, with
    /// the platform's own row drag; <see cref="Moved"/> says where they
    /// went. The list shows nothing moved: the app moves its items.</param>
    /// <param name="itemAnimations">False: rows don't animate in where the
    /// platform animates them (WinUI's list view slides and fades them in,
    /// again when they all change), for lists whose rows move or change
    /// wholesale.</param>
    public ListView(Func<T, string> key, Func<T, Node> row, SelectionMode mode = SelectionMode.Single,
        RowMoves moves = RowMoves.None, bool itemAnimations = true) : base(mode, moves, itemAnimations)
    {
        this.key = key;
        this.row = row;
        FollowItems();
    }

    /// <summary>A list made by <paramref name="create"/>: a table, whose
    /// rows are cells.</summary>
    private protected ListView(Func<T, string> key, Func<Box, uint> create, SelectionMode mode) : base(create, mode)
    {
        this.key = key;
        row = _ => null!;
        FollowItems();
    }

    private void FollowItems()
    {
        // The items usually outlive the list: stop following them with it.
        Forgotten += () =>
        {
            if (_items is INotifyCollectionChanged items) items.CollectionChanged -= OnItemsChanged;
        };
    }

    private protected T? ItemOf(string k) => _byKey.GetValueOrDefault(k);

    private readonly Dictionary<string, T> _byKey = [];
    private IReadOnlyList<T> _items = [];
    private IReadOnlyList<T> _selected = [];

    public IReadOnlyList<T> Items
    {
        get => _items;
        set
        {
            if (_items is INotifyCollectionChanged old) old.CollectionChanged -= OnItemsChanged;
            _items = value;
            if (value is INotifyCollectionChanged now) now.CollectionChanged += OnItemsChanged;
            OnItemsChanged(null, null);
        }
    }

    public IReadOnlyList<T> SelectedItems
    {
        get => _selected;
        set
        {
            _selected = value;
            SetSelectedKeys(value.Select(key).ToArray());
        }
    }

    public T? SelectedItem
    {
        get => _selected.Count > 0 ? _selected[0] : null;
        set => SelectedItems = value is null ? [] : [value];
    }

    /// <summary>Scrolls just enough to show the item's row, shown yet or
    /// not; an item the list doesn't have yet is scrolled to once it has
    /// it (set with the same <see cref="Items"/>).</summary>
    public void ScrollTo(T item) => Native.Check(Native.ListScrollTo(Id, key(item)));

    /// <summary>The user changed the selection.</summary>
    public event Action<IReadOnlyList<T>>? SelectionChanged;

    /// <summary>The user activated a row: double-click or Return.</summary>
    public event Action<T>? Activated;

    /// <summary>The user dragged rows within the list and dropped them: the
    /// items, in the list's order, and where.</summary>
    public event Action<IReadOnlyList<T>, RowDrop<T>>? Moved;

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs? e)
    {
        _byKey.Clear();
        var keys = new string[_items.Count];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = key(_items[i]);
            _byKey[keys[i]] = _items[i];
        }
        SetKeys(keys);
    }

    private protected override Node? Row(string k) => _byKey.TryGetValue(k, out var item) ? row(item) : null;

    private protected override void OnSelectionChanged(IReadOnlyList<string> keys)
    {
        _selected = keys.Select(k => _byKey.GetValueOrDefault(k)).OfType<T>().ToArray();
        SelectionChanged?.Invoke(_selected);
    }

    private protected override void OnMoved(IReadOnlyList<string> keys, RowDropKind kind, string k)
    {
        var items = keys.Select(key => _byKey.GetValueOrDefault(key)).OfType<T>().ToArray();
        var target = kind == RowDropKind.End ? null : _byKey.GetValueOrDefault(k);
        if (items.Length > 0 && (kind == RowDropKind.End || target != null))
            Moved?.Invoke(items, new RowDrop<T>(kind, target));
    }

    private protected override void OnActivated(string k)
    {
        if (_byKey.TryGetValue(k, out var item)) Activated?.Invoke(item);
    }
}
