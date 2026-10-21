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
        Application.Guard(() => node.OnEvent(ev));
    }

    private protected virtual void OnEvent(Native.Event e) { }

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
    }

    // Layout, every node's. Lengths are in points; null is auto.

    private float? _width, _height;

    public float? Width
    {
        set
        {
            _width = value;
            Native.Check(Native.SetSize(Id, _width ?? -1, _height ?? -1));
        }
    }

    public float? Height
    {
        set
        {
            _height = value;
            Native.Check(Native.SetSize(Id, _width ?? -1, _height ?? -1));
        }
    }

    public float Grow { set => Native.Check(Native.SetGrow(Id, value)); }
    public Align AlignSelf { set => Native.Check(Native.SetAlignSelf(Id, (uint)value)); }

    public Thickness Margin
    {
        set => Native.Check(Native.SetMargin(Id, value.Top, value.End, value.Bottom, value.Start));
    }
}

/// <summary>Edges, with start and end following the text's direction.</summary>
public readonly record struct Thickness(float Top, float End, float Bottom, float Start)
{
    public Thickness(float all) : this(all, all, all, all) { }
    public Thickness(float vertical, float horizontal) : this(vertical, horizontal, vertical, horizontal) { }
    public static implicit operator Thickness(float all) => new(all);
}
