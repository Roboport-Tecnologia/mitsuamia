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
        PasswordInput, SearchInput, TextArea, Separator, Spinner, Progress, DocumentTabs, Select, Icon, Split, Tabs, Image, MenuButton, Popover, RadioGroup, SplitButton,
    }

    public enum TextProp : uint { Text, Label, Title, Value, Placeholder, A11yLabel, Tooltip, Icon, TestId }
    public enum BoolProp : uint { Enabled, Checked, TabsClosable, TabsReorderable, NewTabButton, IconOnly, Header, Clickable, SidebarPane, Selectable, PopoverShown, SearchOnReturn }
    public enum NumberProp : uint { Number, EstimatedRowHeight, IconSize, MaxLines }
    public enum EnumProp : uint { TextStyle, ButtonRole, ButtonStyle, SelectionMode, ListStyle, DocumentTabsStyle, HeaderEdge, Orientation, FontWeight, TabsStyle }
    public enum StringsProp : uint { TabTitles, Options }
    public enum IndexProp : uint { SelectedIndex }
    public enum EventKind : uint
    {
        Click, Changed, Submit, FocusIn, FocusOut, CloseRequested, Search, TabCloseRequested, TabMoved, NewTabRequested,
        ContextMenuItem, DropHover, FilesDropped, Pointer, A11yAction, MenuItem, PopoverShownChanged, SuggestionChosen, Key,
    }
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
        public ulong To;
        public float X;
        public float Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Str
    {
        public byte* Ptr;
        public nuint Len;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ListCallbacks
    {
        public delegate* unmanaged[Cdecl]<nint, uint, byte*, nuint, uint> BuildRow;
        public delegate* unmanaged[Cdecl]<nint, uint, uint, void> ReleaseRow;
        public delegate* unmanaged[Cdecl]<nint, uint, void> SelectionChanged;
        public delegate* unmanaged[Cdecl]<nint, uint, byte*, nuint, void> Activated;
        public nint User;
        public uint Moves;
        public delegate* unmanaged[Cdecl]<nint, uint, Str*, nuint, uint, byte*, nuint, void> Moved;
        public uint Still;
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_create_list")]
    public static partial uint CreateList(ListCallbacks callbacks);

    [StructLayout(LayoutKind.Sequential)]
    public struct TableColumn
    {
        public Str Title;
        public float Width;
        [MarshalAs(UnmanagedType.U1)] public bool Expand;
        [MarshalAs(UnmanagedType.U1)] public bool Sortable;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TableCallbacks
    {
        public delegate* unmanaged[Cdecl]<nint, uint, byte*, nuint, nuint, uint> BuildCell;
        public delegate* unmanaged[Cdecl]<nint, uint, uint, void> ReleaseCell;
        public delegate* unmanaged[Cdecl]<nint, uint, void> SelectionChanged;
        public delegate* unmanaged[Cdecl]<nint, uint, byte*, nuint, void> Activated;
        public delegate* unmanaged[Cdecl]<nint, uint, nuint, byte, void> Sorted;
        public nint User;
        public uint Still;
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_create_scroll_view")]
    public static partial uint CreateScrollView(uint axes, uint* content);

    [LibraryImport(Lib, EntryPoint = "mitsuami_create_table")]
    private static partial uint CreateTable(TableCallbacks callbacks, TableColumn* columns, nuint count);

    /// <summary>Columns are titles, widths (0: the platform's), whether
    /// each expands and sorts.</summary>
    public static uint CreateTable(TableCallbacks callbacks, IReadOnlyList<(string Title, float Width, bool Expand, bool Sortable)> columns)
    {
        uint id = 0;
        WithStrs(columns.Select(c => c.Title).ToArray(), (titles, n) =>
        {
            var native = new TableColumn[(int)n];
            for (var i = 0; i < native.Length; i++)
                native[i] = new TableColumn { Title = titles[i], Width = columns[i].Width, Expand = columns[i].Expand, Sortable = columns[i].Sortable };
            fixed (TableColumn* p = native) id = CreateTable(callbacks, p, n);
            return id != 0;
        });
        return id;
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_table_set_sort")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool TableSetSort(uint table, nuint column, [MarshalAs(UnmanagedType.U1)] bool descending);

    [LibraryImport(Lib, EntryPoint = "mitsuami_list_set_keys")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool ListSetKeys(uint list, Str* keys, nuint count);

    [LibraryImport(Lib, EntryPoint = "mitsuami_list_set_selected")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool ListSetSelected(uint list, Str* keys, nuint count);

    [LibraryImport(Lib, EntryPoint = "mitsuami_list_selected_count")]
    public static partial nuint ListSelectedCount(uint list);

    [LibraryImport(Lib, EntryPoint = "mitsuami_list_selected_key")]
    private static partial nuint ListSelectedKey(uint list, nuint index, byte** ptr);

    public static bool ListSetKeys(uint list, IReadOnlyList<string> keys) => WithStrs(keys, (p, n) => ListSetKeys(list, p, n));
    public static bool ListSetSelected(uint list, IReadOnlyList<string> keys) => WithStrs(keys, (p, n) => ListSetSelected(list, p, n));

    [LibraryImport(Lib, EntryPoint = "mitsuami_list_scroll_to")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool ListScrollTo(uint list, Str key);

    public static bool ListScrollTo(uint list, string key)
    {
        string[] keys = [key];
        return WithStrs(keys, (k, _) => ListScrollTo(list, k[0]));
    }

    public static string ListSelectedKey(uint list, nuint index)
    {
        byte* ptr;
        var len = ListSelectedKey(list, index, &ptr);
        return Utf8(ptr, len);
    }

    private delegate bool StrsCall(Str* strs, nuint count);

    /// <summary>Passes strings as UTF-8, pinned for the call.</summary>
    private static bool WithStrs(IReadOnlyList<string> values, StrsCall call)
    {
        var bytes = new byte[values.Count][];
        var handles = new GCHandle[values.Count];
        var strs = new Str[values.Count];
        try
        {
            for (var i = 0; i < values.Count; i++)
            {
                bytes[i] = Encoding.UTF8.GetBytes(values[i]);
                handles[i] = GCHandle.Alloc(bytes[i], GCHandleType.Pinned);
                strs[i] = new Str { Ptr = (byte*)handles[i].AddrOfPinnedObject(), Len = (nuint)bytes[i].Length };
            }
            fixed (Str* p = strs) return call(p, (nuint)strs.Length);
        }
        finally
        {
            foreach (var h in handles) if (h.IsAllocated) h.Free();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Color
    {
        public uint Semantic;
        public byte R, G, B, A;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DrawnCallbacks
    {
        public delegate* unmanaged[Cdecl]<nint, uint, nint, float, float, void> Draw;
        public nint User;
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_create_drawn")]
    private static partial uint CreateDrawn(Str name, DrawnCallbacks callbacks, float width, float height);

    public static uint CreateDrawn(string name, DrawnCallbacks callbacks, float width, float height)
    {
        uint id = 0;
        string[] texts = [name];
        WithStrs(texts, (n, _) => (id = CreateDrawn(n[0], callbacks, width, height)) != 0);
        return id;
    }

    // mitsuami-extras' C ABI, in its library with mitsuami's.

    [LibraryImport(Lib, EntryPoint = "mitsuami_extras_create_text_area")]
    private static partial uint ExtrasCreateTextArea(Str placeholder, uint* area);

    public static (uint Column, uint Area) ExtrasCreateTextArea(string placeholder)
    {
        uint column = 0;
        var area = new uint[1];
        string[] texts = [placeholder];
        WithStrs(texts, (p, _) =>
        {
            fixed (uint* a = area) column = ExtrasCreateTextArea(p[0], a);
            return column != 0;
        });
        return (CheckNode(column), area[0]);
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_extras_text_area_set_value")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool ExtrasTextAreaSetValue(uint column, Str value);

    public static bool ExtrasTextAreaSetValue(uint column, string value)
    {
        string[] texts = [value];
        return WithStrs(texts, (v, _) => ExtrasTextAreaSetValue(column, v[0]));
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_extras_text_area_set_placeholder")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool ExtrasTextAreaSetPlaceholder(uint column, Str placeholder);

    public static bool ExtrasTextAreaSetPlaceholder(uint column, string placeholder)
    {
        string[] texts = [placeholder];
        return WithStrs(texts, (p, _) => ExtrasTextAreaSetPlaceholder(column, p[0]));
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_drawn_set_semantics")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool DrawnSetSemantics(uint node, uint role, Str value, uint actions);

    public static bool DrawnSetSemantics(uint node, uint role, string value, uint actions)
    {
        string[] texts = [value];
        return WithStrs(texts, (v, _) => DrawnSetSemantics(node, role, v[0], actions));
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_drawn_set_size")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool DrawnSetSize(uint node, float width, float height);

    [LibraryImport(Lib, EntryPoint = "mitsuami_drawn_redraw")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool DrawnRedraw(uint node);

    [LibraryImport(Lib, EntryPoint = "mitsuami_canvas_rect")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool CanvasRect(nint canvas, float x, float y, float width, float height, float radius,
        float stroke, Color color);

    [LibraryImport(Lib, EntryPoint = "mitsuami_canvas_ellipse")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool CanvasEllipse(nint canvas, float x, float y, float width, float height, float stroke,
        Color color);

    [LibraryImport(Lib, EntryPoint = "mitsuami_canvas_path")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool CanvasPath(nint canvas, Str data, float viewX, float viewY, float viewWidth,
        float viewHeight, float x, float y, float side, float stroke, Color color);

    public static bool CanvasPath(nint canvas, VectorIcon icon, float x, float y, float side, float stroke, Color color)
    {
        string[] data = [icon.Data];
        var box = icon.ViewBox ?? default;
        return WithStrs(data, (d, _) => CanvasPath(canvas, d[0], box.X, box.Y, box.Width, box.Height, x, y, side, stroke, color));
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

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_vector_icon")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SetVectorIcon(uint node, Str data, float x, float y, float width, float height);

    [LibraryImport(Lib, EntryPoint = "mitsuami_capture_png")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool CapturePng(uint window, Str path, delegate* unmanaged[Cdecl]<nint, byte, void> done, nint user);

    public static bool CapturePng(uint window, string path, delegate* unmanaged[Cdecl]<nint, byte, void> done, nint user)
    {
        string[] texts = [path];
        return WithStrs(texts, (p, _) => CapturePng(window, p[0], done, user));
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_vector_icon_bounds")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool VectorIconBounds(Str data, float* bounds);

    public static ViewBox? VectorIconBounds(string data)
    {
        var bounds = stackalloc float[4];
        string[] texts = [data];
        var b = bounds;
        if (!WithStrs(texts, (d, _) => VectorIconBounds(d[0], b))) return null;
        return new ViewBox(bounds[0], bounds[1], bounds[2], bounds[3]);
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_create_vector_icon")]
    private static partial uint CreateVectorIcon(Str data, float x, float y, float width, float height);

    public static uint CreateVectorIcon(VectorIcon icon)
    {
        uint id = 0;
        string[] data = [icon.Data];
        var box = icon.ViewBox ?? default;
        WithStrs(data, (d, _) => (id = CreateVectorIcon(d[0], box.X, box.Y, box.Width, box.Height)) != 0);
        return id;
    }

    public static bool SetVectorIcon(uint node, VectorIcon? icon)
    {
        string[] data = [icon?.Data ?? ""];
        var box = icon?.ViewBox ?? default;
        return WithStrs(data, (d, _) => SetVectorIcon(node, d[0], box.X, box.Y, box.Width, box.Height));
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_image")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetImage(uint node, uint width, uint height, float scale, byte* rgba, nuint len);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_clipboard_text")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SetClipboardText(Str text);

    public static bool SetClipboardText(string text)
    {
        string[] texts = [text];
        return WithStrs(texts, (t, _) => SetClipboardText(t[0]));
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_launch")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool Launch(uint window, Str target, [MarshalAs(UnmanagedType.U1)] bool url);

    public static bool Launch(uint window, string target, bool url)
    {
        string[] texts = [target];
        return WithStrs(texts, (t, _) => Launch(window, t[0], url));
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_modal")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetModal(uint window, uint owner, uint modality);

    [LibraryImport(Lib, EntryPoint = "mitsuami_alert")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool Alert(uint window, Str title, Str message, Str* buttons, nuint count, uint style,
        delegate* unmanaged[Cdecl]<nint, nuint, void> answer, nint user);

    [LibraryImport(Lib, EntryPoint = "mitsuami_open_file")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool OpenFile(uint window, Str title, [MarshalAs(UnmanagedType.U1)] bool folders,
        [MarshalAs(UnmanagedType.U1)] bool multiple, Str startFolder,
        delegate* unmanaged[Cdecl]<nint, Str*, nuint, void> answer, nint user);

    [LibraryImport(Lib, EntryPoint = "mitsuami_save_file")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SaveFile(uint window, Str title, Str name, Str startFolder,
        delegate* unmanaged[Cdecl]<nint, Str*, nuint, void> answer, nint user);

    public static bool Alert(uint window, string title, string message, IReadOnlyList<string> buttons, uint style,
        delegate* unmanaged[Cdecl]<nint, nuint, void> answer, nint user)
    {
        string[] texts = [title, message];
        return WithStrs(texts, (t, _) => WithStrs(buttons, (b, n) => Alert(window, t[0], t[1], b, n, style, answer, user)));
    }

    public static bool OpenFile(uint window, string title, bool folders, bool multiple, string startFolder,
        delegate* unmanaged[Cdecl]<nint, Str*, nuint, void> answer, nint user)
    {
        string[] texts = [title, startFolder];
        return WithStrs(texts, (t, _) => OpenFile(window, t[0], folders, multiple, t[1], answer, user));
    }

    public static bool SaveFile(uint window, string title, string name, string startFolder,
        delegate* unmanaged[Cdecl]<nint, Str*, nuint, void> answer, nint user)
    {
        string[] texts = [title, name, startFolder];
        return WithStrs(texts, (t, _) => SaveFile(window, t[0], t[1], t[2], answer, user));
    }

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

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_strings")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SetStrings(uint node, StringsProp prop, Str* values, nuint count);

    public static bool SetStrings(uint node, StringsProp prop, IReadOnlyList<string> values) =>
        WithStrs(values, (p, n) => SetStrings(node, prop, p, n));

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_index")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetIndex(uint node, IndexProp prop, nint index);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_enum")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetEnum(uint node, EnumProp prop, uint value);

    [StructLayout(LayoutKind.Sequential)]
    public struct MenuEntry
    {
        public uint Kind;
        public uint Id;
        public Str Title;
        public byte Enabled;
        public uint Check;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Shortcut
    {
        public uint Key;
        public uint Ch;
        public byte Primary;
        public byte Shift;
        public byte Alt;
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_keys")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SetKeys(uint node, Shortcut* keys, nuint count);

    public static bool SetKeys(uint node, IReadOnlyList<Mitsuamia.Shortcut> keys)
    {
        var native = keys.Select(k => new Shortcut
        {
            Key = (uint)k.Key,
            Ch = k.Code,
            Primary = k.Primary ? (byte)1 : (byte)0,
            Shift = k.Shift ? (byte)1 : (byte)0,
            Alt = k.Alt ? (byte)1 : (byte)0,
        }).ToArray();
        fixed (Shortcut* p = native) return SetKeys(node, p, (nuint)native.Length);
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_context_menu")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SetContextMenu(uint node, MenuEntry* entries, nuint count);

    /// <summary>Entries with their titles; the titles are passed as UTF-8,
    /// pinned for the call.</summary>
    [LibraryImport(Lib, EntryPoint = "mitsuami_set_context_menu_on_open")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetContextMenuOnOpen(uint node, delegate* unmanaged[Cdecl]<nint, uint, void> build, nint user);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_menu_on_open")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetMenuOnOpen(uint node, delegate* unmanaged[Cdecl]<nint, uint, void> build, nint user);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_suggestions")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SetSuggestions(uint node, Str* titles, Str* details, nuint count);

    public static bool SetSuggestions(uint node, IReadOnlyList<(string Title, string? Detail)> suggestions)
    {
        var titles = suggestions.Select(s => s.Title).ToArray();
        var details = suggestions.Select(s => s.Detail ?? "").ToArray();
        return WithStrs(titles, (t, n) => WithStrs(details, (d, _) => SetSuggestions(node, t, d, n)));
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_menu")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SetMenu(uint node, MenuEntry* entries, nuint count);

    public static bool SetContextMenu(uint node, IReadOnlyList<(uint Kind, uint Id, string Title, bool Enabled, uint Check)> entries) =>
        SetMenuEntries(node, entries, button: false);

    /// <summary>A menu button's menu, as <see cref="SetContextMenu"/>'s entries.</summary>
    public static bool SetMenu(uint node, IReadOnlyList<(uint Kind, uint Id, string Title, bool Enabled, uint Check)> entries) =>
        SetMenuEntries(node, entries, button: true);

    private static bool SetMenuEntries(uint node, IReadOnlyList<(uint Kind, uint Id, string Title, bool Enabled, uint Check)> entries, bool button)
    {
        var titles = entries.Select(e => e.Title).ToArray();
        return WithStrs(titles, (strs, _) =>
        {
            var native = new MenuEntry[entries.Count];
            for (var i = 0; i < native.Length; i++)
            {
                var e = entries[i];
                native[i] = new MenuEntry { Kind = e.Kind, Id = e.Id, Title = strs[i], Enabled = (byte)(e.Enabled ? 1 : 0), Check = e.Check };
            }
            fixed (MenuEntry* p = native) return button ? SetMenu(node, p, (nuint)native.Length) : SetContextMenu(node, p, (nuint)native.Length);
        });
    }

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_file_drop")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SetFileDrop(uint node, [MarshalAs(UnmanagedType.U1)] bool folders,
        [MarshalAs(UnmanagedType.U1)] bool anyFile, Str* extensions, nuint count);

    public static bool SetFileDrop(uint node, bool folders, bool anyFile, IReadOnlyList<string> extensions) =>
        WithStrs(extensions, (p, n) => SetFileDrop(node, folders, anyFile, p, n));

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_color")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetColor(uint node, uint prop, uint semantic, byte r, byte g, byte b, byte a);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_min_size")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetMinSize(uint node, Length width, Length height);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_max_size")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetMaxSize(uint node, Length width, Length height);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_hidden")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetHidden(uint node, [MarshalAs(UnmanagedType.U1)] bool hidden);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_direction")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetDirection(uint node, [MarshalAs(UnmanagedType.U1)] bool row);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_gap")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetGap(uint node, Length gap);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_padding")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetPadding(uint node, Length top, Length end, Length bottom, Length start);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_position")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetPosition(uint node, [MarshalAs(UnmanagedType.U1)] bool absolute, Length top, Length end, Length bottom, Length start);

    [LibraryImport(Lib, EntryPoint = "mitsuami_frame")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool Frame(uint node, float* frame);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_margin")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetMargin(uint node, Length top, Length end, Length bottom, Length start);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_size")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetSize(uint node, Length width, Length height);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_grow")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetGrow(uint node, float grow);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_wrap")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetWrap(uint node, [MarshalAs(UnmanagedType.U1)] bool wrap);

    [LibraryImport(Lib, EntryPoint = "mitsuami_set_shrink")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetShrink(uint node, float shrink);

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
