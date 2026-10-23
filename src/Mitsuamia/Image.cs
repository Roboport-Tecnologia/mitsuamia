namespace Mitsuamia;

/// <summary>
/// The platform's image view, showing pixels the app decoded. It's as big
/// as its pixels over their scale, unless the layout sizes it, and then
/// fits them in keeping their proportions.
/// </summary>
public sealed unsafe class Image : Node
{
    public Image() : base(Native.Kind.Image) { }

    /// <summary>Shows <paramref name="width"/> × <paramref name="height"/>
    /// pixels of straight (not premultiplied) RGBA8, top row first,
    /// <paramref name="scale"/> pixels to a point.</summary>
    public void SetPixels(int width, int height, ReadOnlySpan<byte> rgba, float scale = 1)
    {
        fixed (byte* p = rgba)
            Native.Check(Native.SetImage(Id, (uint)width, (uint)height, scale, p, (nuint)rgba.Length));
    }

    /// <summary>What it shows, read by assistive technology; without one
    /// it's decorative.</summary>
    public string Label { set => Native.Check(Native.SetText(Id, Native.TextProp.Label, value)); }
}
