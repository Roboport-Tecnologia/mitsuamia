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

    /// <summary>Another of the app's icons in its place. One made with a
    /// name keeps it on WinUI, which picks an icon's element by its first
    /// props.</summary>
    public VectorIcon Vector { set => Native.Check(Native.SetVectorIcon(Id, value)); }

    /// <summary>Its side, in points; otherwise the platform's size for an
    /// icon on its own.</summary>
    public float Size { set => Native.Check(Native.SetNumber(Id, Native.NumberProp.IconSize, value)); }

    /// <summary>Its colour, in place of the one the platform gives icons.</summary>
    public Color Color { set => Native.Check(value.Set(Id)); }

    /// <summary>What it stands for, read by assistive technology; without
    /// one it's decorative.</summary>
    public string Label { set => Native.Check(Native.SetText(Id, Native.TextProp.Label, value)); }
}

/// <summary>Where a <see cref="FileIcon"/> gets its icon.</summary>
public enum FileLookup : uint
{
    /// <summary>The file or folder on disk: an app's own icon, a folder's
    /// custom one, a document's by its type.</summary>
    Disk,
    /// <summary>A file of the type its name says, with nothing read: for
    /// files that aren't on disk.</summary>
    FileName,
    /// <summary>A plain folder, with nothing read.</summary>
    Folder,
}

/// <summary>A file's or folder's own icon, as the platform's file manager
/// shows it: a folder's, a document's by its type. A path that doesn't
/// exist shows a generic document on macOS and nothing elsewhere, unless
/// it goes by its name (<see cref="Lookup"/>).</summary>
public sealed class FileIcon : Node
{
    public FileIcon(string path, FileLookup lookup = FileLookup.Disk) : base(Native.Kind.FileIcon)
    {
        if (lookup != FileLookup.Disk)
            Lookup = lookup;
        Path = path;
    }

    /// <summary>Where its icon comes from: the file on disk, or only its
    /// name.</summary>
    public FileLookup Lookup { set => Native.Check(Native.SetEnum(Id, Native.EnumProp.FileLookup, (uint)value)); }

    /// <summary>The file or folder, read when it's set.</summary>
    public string Path { set => Native.Check(Native.SetText(Id, Native.TextProp.File, value)); }

    /// <summary>Its square's side, in points; otherwise the platform's
    /// small icons' (16).</summary>
    public float Size { set => Native.Check(Native.SetNumber(Id, Native.NumberProp.IconSize, value)); }
}
