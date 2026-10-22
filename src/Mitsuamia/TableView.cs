using System.Runtime.InteropServices;

namespace Mitsuamia;

/// <summary>A column of a <see cref="TableView{T}"/>: its header's title,
/// the node each item shows in its cell (built when the platform shows its
/// row), the width it starts at (0: the platform's), whether it takes the
/// room left, and whether pressing its header sorts.</summary>
public sealed record TableColumn<T>(string Title, Func<T, Node> Cell, float Width = 0, bool Expand = false,
    bool Sortable = false);

/// <summary>
/// The platform's own table control: rows of cells under column headers,
/// kept in step with an observable collection as a <see cref="ListView{T}"/>
/// is. The user resizes the columns, as the platform lets them; the app
/// sorts its items when <see cref="Sorted"/> says.
/// </summary>
public sealed unsafe class TableView<T> : ListView<T> where T : class
{
    private readonly IReadOnlyList<TableColumn<T>> columns;

    /// <param name="itemAnimations">As a list's.</param>
    public TableView(Func<T, string> key, IReadOnlyList<TableColumn<T>> columns,
        SelectionMode mode = SelectionMode.Single, bool itemAnimations = true)
        : base(key, box => Create(box, columns, itemAnimations), mode)
    {
        this.columns = columns;
    }

    private static uint Create(Box box, IReadOnlyList<TableColumn<T>> columns, bool itemAnimations)
    {
        var callbacks = new Native.TableCallbacks
        {
            BuildCell = &BuildCell,
            ReleaseCell = &ReleaseRow,
            SelectionChanged = &SelectionChangedCallback,
            Activated = &ActivatedCallback,
            Sorted = &SortedCallback,
            // Lives as long as the app: tables are few.
            User = GCHandle.ToIntPtr(GCHandle.Alloc(box)),
            Still = itemAnimations ? 0u : 1u,
        };
        var data = columns.Select(c => (c.Title, c.Width, c.Expand, c.Sortable)).ToArray();
        return Native.CheckNode(Native.CreateTable(callbacks, data));
    }

    /// <summary>Shows the table sorted by a sortable column; tables with
    /// sortable columns start sorted by the first, ascending.</summary>
    public void SetSort(int column, bool descending) =>
        Native.Check(Native.TableSetSort(Id, (nuint)column, descending));

    /// <summary>The user pressed a sortable header: the column and whether
    /// it's descending. The app sorts its items.</summary>
    public event Action<int, bool>? Sorted;

    private protected override Node? Cell(string key, int column) =>
        ItemOf(key) is { } item && column < columns.Count ? columns[column].Cell(item) : null;

    private protected override void OnSorted(int column, bool descending) => Sorted?.Invoke(column, descending);
}
