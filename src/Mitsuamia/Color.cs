namespace Mitsuamia;

/// <summary>
/// A colour: one of the platform's own, which follow dark mode, high
/// contrast and the accent, or a fixed one. Prefer the platform's.
/// </summary>
public readonly record struct Color
{
    private readonly uint _semantic;

    private Color(uint semantic, byte r = 0, byte g = 0, byte b = 0, byte a = 0)
    {
        _semantic = semantic;
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte A { get; }

    public static Color Label { get; } = new(0);
    /// <summary>For less important text: a path beside a name, a caption.</summary>
    public static Color SecondaryLabel { get; } = new(1);
    public static Color Accent { get; } = new(2);
    public static Color Separator { get; } = new(3);
    public static Color ControlBackground { get; } = new(4);
    public static Color WindowBackground { get; } = new(5);
    public static Color Error { get; } = new(6);
    public static Color Warning { get; } = new(7);
    public static Color Success { get; } = new(8);

    /// <summary>A fixed colour, the same in light and dark.</summary>
    public static Color Rgba(byte r, byte g, byte b, byte a = 255) => new(9, r, g, b, a);

    internal bool Set(uint node) => Native.SetColor(node, 0, _semantic, R, G, B, A);
}
