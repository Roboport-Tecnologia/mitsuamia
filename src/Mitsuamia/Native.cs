using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Mitsuamia;

/// <summary>
/// mitsuami's C ABI (the <c>mitsuami-ffi</c> crate), one to one. Strings
/// go as UTF-8, a pointer and a length; nodes as their ids.
/// </summary>
internal static unsafe partial class Native
{
    private const string Lib = "mitsuami_ffi";

    static Native()
    {
        // MITSUAMI_FFI names the library while there's no package that
        // ships it next to the app.
        NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, (name, assembly, path) =>
        {
            if (name != Lib) return 0;
            var file = Environment.GetEnvironmentVariable("MITSUAMI_FFI");
            if (file is not null && NativeLibrary.TryLoad(file, out var handle)) return handle;
            return NativeLibrary.TryLoad(name, assembly, path, out handle) ? handle : 0;
        });
    }

    public enum Kind : uint
    {
        Container, Text, Button, ToggleButton, Checkbox, Switch, TextInput,
        PasswordInput, SearchInput, TextArea, Separator, Spinner, Progress,
    }

    public enum TextProp : uint { Text, Label, Title, Value, Placeholder, A11yLabel }
    public enum BoolProp : uint { Enabled, Checked }
    public enum NumberProp : uint { Number }
    public enum EnumProp : uint { TextStyle, ButtonRole, ButtonStyle }
    public enum EventKind : uint { Click, Changed, Submit, FocusIn, FocusOut, CloseRequested }
    public enum ValueKind : uint { None, Text, Bool, Number, Index }

    [StructLayout(LayoutKind.Sequential)]
    public struct Event
    {
        public EventKind Kind;
        public ValueKind ValueKind;
        public byte* Text;
        public nuint TextLen;
        [MarshalAs(UnmanagedType.U1)] public bool Flag;
        public double Number;
        public ulong Index;
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_last_error")]
    private static partial nuint LastError(byte** ptr);

    [LibraryImport(Lib, EntryPoint = "mitsuami_run")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool Run(byte* id, nuint idLen, byte* name, nuint nameLen,
        delegate* unmanaged[Cdecl]<nint, void> setup, nint user);

    [LibraryImport(Lib, EntryPoint = "mitsuami_post")]
    public static partial void Post(delegate* unmanaged[Cdecl]<nint, void> callback, nint user);

    [LibraryImport(Lib, EntryPoint = "mitsuami_create")]
    public static partial uint Create(Kind kind);

    [LibraryImport(Lib, EntryPoint = "mitsuami_create_window")]
    public static partial uint CreateWindow(byte* title, nuint titleLen, float width, float height);

    [LibraryImport(Lib, EntryPoint = "mitsuami_insert_child")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool InsertChild(uint parent, uint child, nuint index);

    [LibraryImport(Lib, EntryPoint = "mitsuami_append_child")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool AppendChild(uint parent, uint child);

    [LibraryImport(Lib, EntryPoint = "mitsuami_destroy")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool Destroy(uint node);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_text")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SetText(uint node, TextProp prop, byte* value, nuint len);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_bool")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetBool(uint node, BoolProp prop, [MarshalAs(UnmanagedType.U1)] bool value);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_number")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetNumber(uint node, NumberProp prop, double value);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_enum")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetEnum(uint node, EnumProp prop, uint value);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_direction")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetDirection(uint node, [MarshalAs(UnmanagedType.U1)] bool row);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_gap")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetGap(uint node, float gap);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_padding")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetPadding(uint node, float top, float end, float bottom, float start);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_margin")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetMargin(uint node, float top, float end, float bottom, float start);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_size")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetSize(uint node, float width, float height);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_grow")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetGrow(uint node, float grow);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_align_items")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetAlignItems(uint node, uint align);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_align_self")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetAlignSelf(uint node, uint align);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_justify")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetJustify(uint node, uint justify);

    [LibraryImport(Lib, EntryPoint = "mitsuami_on_event")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool OnEvent(uint node, delegate* unmanaged[Cdecl]<nint, uint, Event*, void> callback,
        nint user);

    public static bool SetText(uint node, TextProp prop, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        fixed (byte* p = bytes) return SetText(node, prop, p, (nuint)bytes.Length);
    }

    public static string Utf8(byte* ptr, nuint len) => len == 0 ? "" : Encoding.UTF8.GetString(ptr, checked((int)len));

    /// <summary>Throws if a call failed, with mitsuami's reason.</summary>
    public static void Check(bool ok, [CallerArgumentExpression(nameof(ok))] string? call = null)
    {
        if (ok) return;
        byte* ptr;
        var len = LastError(&ptr);
        throw new MitsuamiException($"{call}: {Utf8(ptr, len)}");
    }

    public static uint CheckNode(uint node, [CallerArgumentExpression(nameof(node))] string? call = null)
    {
        Check(node != 0, call);
        return node;
    }
}

public sealed class MitsuamiException(string message) : Exception(message);
