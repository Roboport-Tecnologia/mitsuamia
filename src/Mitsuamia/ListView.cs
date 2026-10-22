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
    private sealed class Box { public ListView? List; }

    private readonly Dictionary<uint, Node> _rows = [];

    private protected ListView(SelectionMode mode, RowMoves moves, bool itemAnimations)
        : base(Create(moves, itemAnimations, out var box))
    {
        box.List = this;
        SelectionMode = mode;
    }

    private static uint Create(RowMoves moves, bool itemAnimations, out Box box)
    {
        box = new Box();
        var callbacks = new Native.ListCallbacks
        {
            BuildRow = &BuildRow,
            ReleaseRow = &ReleaseRow,
            SelectionChanged = &SelectionChangedCallback,
            Activated = &Activated,
            // Lives as long as the app: lists are few.
            User = GCHandle.ToIntPtr(GCHandle.Alloc(box)),
            Moves = (uint)moves,
            Moved = &MovedCallback,
            Still = itemAnimations ? 0u : 1u,
        };
        return Native.CheckNode(Native.CreateList(callbacks));
    }

    private static ListView? Of(nint user) => ((Box)GCHandle.FromIntPtr(user).Target!).List;

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
    private protected abstract void OnSelectionChanged(IReadOnlyList<string> keys);
    private protected abstract void OnActivated(string key);
    private protected abstract void OnMoved(IReadOnlyList<string> keys, RowDropKind kind, string key);

    private protected void SetKeys(IReadOnlyList<string> keys) => Native.Check(Native.ListSetKeys(Id, keys));
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
    private static void ReleaseRow(nint user, uint list, uint row)
    {
        Application.Guard(() =>
        {
            // mitsuami destroyed the row's nodes itself.
            if (Of(user) is { } view && view._rows.Remove(row, out var node)) node.Forget();
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void SelectionChangedCallback(nint user, uint list)
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
    private static void Activated(nint user, uint list, byte* key, nuint len)
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
public sealed class ListView<T> : ListView where T : class
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
        // The items usually outlive the list: stop following them with it.
        Forgotten += () =>
        {
            if (_items is INotifyCollectionChanged items) items.CollectionChanged -= ItemsChanged;
        };
    }

    private readonly Dictionary<string, T> _byKey = [];
    private IReadOnlyList<T> _items = [];
    private IReadOnlyList<T> _selected = [];

    public IReadOnlyList<T> Items
    {
        get => _items;
        set
        {
            if (_items is INotifyCollectionChanged old) old.CollectionChanged -= ItemsChanged;
            _items = value;
            if (value is INotifyCollectionChanged now) now.CollectionChanged += ItemsChanged;
            ItemsChanged(null, null);
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

    /// <summary>The user changed the selection.</summary>
    public event Action<IReadOnlyList<T>>? SelectionChanged;

    /// <summary>The user activated a row: double-click or Return.</summary>
    public event Action<T>? Activated;

    /// <summary>The user dragged rows within the list and dropped them: the
    /// items, in the list's order, and where.</summary>
    public event Action<IReadOnlyList<T>, RowDrop<T>>? Moved;

    private void ItemsChanged(object? sender, NotifyCollectionChangedEventArgs? e)
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
