namespace Mitsuamia;

/// <summary>A rectangle in an icon's own units.</summary>
public readonly record struct ViewBox(float X, float Y, float Width, float Height);

/// <summary>
/// An icon the app draws, as path data in the SVG syntax, which XAML's and
/// Avalonia's geometries use too (a leading <c>F0</c> or <c>F1</c> sets the
/// fill rule). It's drawn the way each platform draws its symbolic icons, so
/// it takes their colour and follows dark mode. Without a view box, the path
/// fills its own bounds, as a XAML <c>Path</c> with uniform stretch does.
/// </summary>
public sealed record VectorIcon(string Data, ViewBox? ViewBox = null)
{
    /// <summary>The bounds of the path's shapes, in its own units; null if
    /// the data doesn't parse. To pad an icon in a view box, so it shows
    /// smaller than its square, or off its centre, as icon sets balance
    /// their shapes.</summary>
    public static ViewBox? Bounds(string data) => Native.VectorIconBounds(data);
}

/// <summary>An icon on its own: the platform's, by name, or the app's.</summary>
public sealed class Icon : Node
{
    // Made with its icon: WinUI picks an icon's element by its first props.
    public Icon(VectorIcon icon) : base(Native.CheckNode(Native.CreateVectorIcon(icon))) { }

    public Icon(string name) : base(Native.Kind.Icon) => Native.Check(Native.SetText(Id, Native.TextProp.Icon, name));

    /// <summary>Its side, in points; otherwise the platform's size for an
    /// icon on its own.</summary>
    public float Size { set => Native.Check(Native.SetNumber(Id, Native.NumberProp.IconSize, value)); }

    /// <summary>What it stands for, read by assistive technology; without
    /// one it's decorative.</summary>
    public string Label { set => Native.Check(Native.SetText(Id, Native.TextProp.Label, value)); }
}
