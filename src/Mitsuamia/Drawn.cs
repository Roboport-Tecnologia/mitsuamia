using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mitsuamia;

/// <summary>What a drawn widget is to assistive technology.</summary>
public enum DrawnRole : uint { Group, Image, Button, Slider }

/// <summary>The accessibility actions a drawn widget takes.</summary>
[Flags]
public enum DrawnActions : uint { None = 0, Activate = 1, SetValue = 2, Increment = 4, Decrement = 8 }

/// <summary>
/// A widget the app draws itself, for a look no platform has: mitsuami's
/// drawn tier. It's laid out as any node, draws in the platform's colours
/// (so it follows dark mode and the accent), and hears the primary button
/// and assistive technology's actions. Draw it the same on every platform;
/// a widget a platform has belongs on its native control instead.
/// </summary>
public abstract unsafe class DrawnWidget : Node
{
    private sealed class Box { public DrawnWidget? Widget; }

    /// <param name="name">What trees and snapshots call it.</param>
    /// <param name="width">Its size, in points, unless its style sizes it.</param>
    protected DrawnWidget(string name, float width, float height) : base(Create(name, width, height, out var box))
    {
        box.Widget = this;
    }

    private static uint Create(string name, float width, float height, out Box box)
    {
        box = new Box();
        var callbacks = new Native.DrawnCallbacks
        {
            Draw = &DrawCallback,
            // Lives as long as the app: drawn widgets are few.
            User = GCHandle.ToIntPtr(GCHandle.Alloc(box)),
        };
        return Native.CheckNode(Native.CreateDrawn(name, callbacks, width, height));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void DrawCallback(nint user, uint node, nint canvas, float width, float height)
    {
        if (((Box)GCHandle.FromIntPtr(user).Target!).Widget is not { } widget) return;
        Application.Guard(() => widget.Draw(new Canvas(canvas, width, height)));
    }

    /// <summary>Draws it. mitsuami is laying out: draw on the canvas, and
    /// change nothing else. It's called again on a new size, and after
    /// <see cref="Redraw"/>.</summary>
    protected abstract void Draw(Canvas canvas);

    /// <summary>Draw it again: what it shows has changed.</summary>
    protected void Redraw() => Native.Check(Native.DrawnRedraw(Id));

    /// <summary>Its size, in points, unless its style sizes it.</summary>
    protected void SetSize(float width, float height) => Native.Check(Native.DrawnSetSize(Id, width, height));

    /// <summary>What it is to assistive technology: a role, its value as
    /// read out, and the actions it takes, which come to
    /// <see cref="OnAction"/>.</summary>
    protected void SetSemantics(DrawnRole role, string? value, DrawnActions actions) =>
        Native.Check(Native.DrawnSetSemantics(Id, (uint)role, value ?? "", (uint)actions));

    /// <summary>Its name, read by assistive technology.</summary>
    public string Label { set => Native.Check(Native.SetText(Id, Native.TextProp.A11yLabel, value)); }

    /// <summary>The primary button went down on it, at this point in its
    /// coordinates.</summary>
    protected virtual void OnPointerPressed(float x, float y) { }

    protected virtual void OnPointerReleased(float x, float y) { }

    /// <summary>Assistive technology (or a test) acted on it: one of the
    /// actions it takes, with the value set for <c>SetValue</c>.</summary>
    protected virtual void OnAction(DrawnActions action, string? value) { }

    private protected override void OnEvent(Native.Event e)
    {
        switch (e.Kind)
        {
            case Native.EventKind.Pointer when e.Flag:
                OnPointerPressed(e.X, e.Y);
                break;
            case Native.EventKind.Pointer:
                OnPointerReleased(e.X, e.Y);
                break;
            case Native.EventKind.A11yAction:
                var value = e.ValueKind == Native.ValueKind.Text ? Native.Utf8(e.Text, e.TextLen) : null;
                OnAction((DrawnActions)e.Index, value);
                break;
        }
    }
}

/// <summary>
/// Where a drawn widget draws, in points from its top-left corner. It's
/// only valid while <see cref="DrawnWidget.Draw"/> runs.
/// </summary>
public readonly ref struct Canvas
{
    private readonly nint _canvas;

    internal Canvas(nint canvas, float width, float height)
    {
        _canvas = canvas;
        Width = width;
        Height = height;
    }

    /// <summary>The widget's laid-out size.</summary>
    public float Width { get; }
    public float Height { get; }

    public void FillRect(float x, float y, float width, float height, Color color, float radius = 0) =>
        Native.Check(Native.CanvasRect(_canvas, x, y, width, height, radius, 0, color.ToNative()));

    /// <summary>Strokes a rectangle's outline, <paramref name="lineWidth"/>
    /// points wide, centred on its edge.</summary>
    public void StrokeRect(float x, float y, float width, float height, Color color, float lineWidth = 1, float radius = 0) =>
        Native.Check(Native.CanvasRect(_canvas, x, y, width, height, radius, lineWidth, color.ToNative()));

    public void FillEllipse(float x, float y, float width, float height, Color color) =>
        Native.Check(Native.CanvasEllipse(_canvas, x, y, width, height, 0, color.ToNative()));

    public void StrokeEllipse(float x, float y, float width, float height, Color color, float lineWidth = 1) =>
        Native.Check(Native.CanvasEllipse(_canvas, x, y, width, height, lineWidth, color.ToNative()));

    /// <summary>Fills an icon's path in a square of <paramref name="side"/>
    /// points at <paramref name="x"/>, <paramref name="y"/>, fitted as an
    /// <see cref="Icon"/> of that size is. The non-zero rule fills it.</summary>
    public void FillIcon(VectorIcon icon, float x, float y, float side, Color color) =>
        Native.Check(Native.CanvasPath(_canvas, icon, x, y, side, 0, color.ToNative()));

    /// <summary>Strokes a path, given as SVG path data in the widget's own
    /// points, with lines <paramref name="lineWidth"/> points wide.</summary>
    public void StrokePath(string data, Color color, float lineWidth = 1) =>
        Native.Check(Native.CanvasPath(_canvas, InPoints(data), 0, 0, Side, lineWidth, color.ToNative()));

    /// <summary>Fills a path, as SVG path data in the widget's own points,
    /// by the non-zero rule.</summary>
    public void FillPath(string data, Color color) =>
        Native.Check(Native.CanvasPath(_canvas, InPoints(data), 0, 0, Side, 0, color.ToNative()));

    // A square view box as large as the square it's fitted in draws the
    // path's units as points.
    private float Side => Math.Max(1, Math.Max(Width, Height));

    private VectorIcon InPoints(string data) => new(data, new ViewBox(0, 0, Side, Side));
}
