using System.Runtime.InteropServices;

namespace Mitsuamia;

/// <summary>The platform's spacing: each platform says what each step is,
/// so layouts follow its design language. <see cref="Indent"/> is how far
/// its trees indent each level.</summary>
public enum Spacing : uint { None, Xs, Sm, Md, Lg, Xl, Indent }

/// <summary>
/// A length: points (a plain number), a platform <see cref="Spacing"/> step,
/// a percentage of the container, or the font's size (<c>em</c>, <c>rem</c>).
/// <c>default</c> is auto.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Length
{
    private readonly uint _unit;
    private readonly float _value;

    private Length(uint unit, float value)
    {
        _unit = unit;
        _value = value;
    }

    public static Length Auto => default;
    public static Length Points(float points) => new(1, points);
    public static Length Token(Spacing spacing) => new(2, (float)spacing);
    public static Length Percent(float percent) => new(3, percent);
    /// <summary>Times the node's font size.</summary>
    public static Length Em(float times) => new(4, times);
    /// <summary>Times the platform's body font size, which follows the
    /// user's text size.</summary>
    public static Length Rem(float times) => new(5, times);

    public static implicit operator Length(float points) => Points(points);
    public static implicit operator Length(Spacing spacing) => Token(spacing);
}
