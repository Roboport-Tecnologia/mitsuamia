using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mitsuamia;

public enum AlertStyle : uint { Info, Warning, Critical }

/// <summary>
/// The platform's alerts and file dialogs, as tasks: they never block, and
/// answer on the UI thread. On macOS they're sheets on their window.
/// </summary>
public static unsafe class Dialogs
{
    /// <summary>Shows an alert; the buttons in order of importance, the
    /// first the default. Answers the index of the button chosen.</summary>
    public static Task<int> Alert(Window? on, string title, string message, IReadOnlyList<string> buttons,
        AlertStyle style = AlertStyle.Info)
    {
        Application.VerifyAccess();
        var answer = new TaskCompletionSource<int>();
        var user = GCHandle.ToIntPtr(GCHandle.Alloc(answer));
        Native.Check(Native.Alert(on?.Id ?? 0, title, message, buttons, (uint)style, &Answered, user));
        return answer.Task;
    }

    /// <summary>Asks for a folder; null if cancelled.</summary>
    public static Task<string?> OpenFolder(Window? on, string title = "", string startFolder = "") =>
        Open(on, title, folders: true, multiple: false, startFolder)
            .ContinueWith(t => t.Result.Length > 0 ? t.Result[0] : null, TaskContinuationOptions.ExecuteSynchronously);

    /// <summary>Asks for files; none if cancelled.</summary>
    public static Task<string[]> OpenFiles(Window? on, string title = "", bool multiple = false, string startFolder = "") =>
        Open(on, title, folders: false, multiple, startFolder);

    private static Task<string[]> Open(Window? on, string title, bool folders, bool multiple, string startFolder)
    {
        Application.VerifyAccess();
        var answer = new TaskCompletionSource<string[]>();
        var user = GCHandle.ToIntPtr(GCHandle.Alloc(answer));
        Native.Check(Native.OpenFile(on?.Id ?? 0, title, folders, multiple, startFolder, &Opened, user));
        return answer.Task;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Answered(nint user, nuint index)
    {
        var handle = GCHandle.FromIntPtr(user);
        var answer = (TaskCompletionSource<int>)handle.Target!;
        handle.Free();
        answer.SetResult((int)index);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Opened(nint user, Native.Str* paths, nuint count)
    {
        var handle = GCHandle.FromIntPtr(user);
        var answer = (TaskCompletionSource<string[]>)handle.Target!;
        handle.Free();
        var chosen = new string[(int)count];
        for (var i = 0; i < chosen.Length; i++) chosen[i] = Native.Utf8(paths[i].Ptr, paths[i].Len);
        answer.SetResult(chosen);
    }
}
