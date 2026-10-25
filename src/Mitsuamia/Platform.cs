using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mitsuamia;

/// <summary>The platform's clipboard.</summary>
public static class Clipboard
{
    /// <summary>Puts text on it. A write the platform refuses is only
    /// logged.</summary>
    public static void SetText(string text)
    {
        Application.VerifyAccess();
        Native.Check(Native.SetClipboardText(text));
    }

    /// <summary>Its text, or null when it holds none.</summary>
    public static unsafe Task<string?> GetText()
    {
        Application.VerifyAccess();
        var answer = new TaskCompletionSource<string?>();
        var user = GCHandle.ToIntPtr(GCHandle.Alloc(answer));
        Native.Check(Native.ClipboardText(&Answered, user));
        return answer.Task;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void Answered(nint user, Native.Str* text, nuint count)
    {
        var handle = GCHandle.FromIntPtr(user);
        var answer = (TaskCompletionSource<string?>)handle.Target!;
        handle.Free();
        answer.SetResult(count > 0 ? Native.Utf8(text[0].Ptr, text[0].Len) : null);
    }
}

/// <summary>Opens links and files in other apps, as the platform does.
/// A launch that fails is only logged.</summary>
public static class Launch
{
    /// <summary>A URL in the app set for its scheme: the browser for
    /// <c>https:</c>, the mail app for <c>mailto:</c>.</summary>
    public static void Url(string url, Window? on = null)
    {
        Application.VerifyAccess();
        Native.Check(Native.Launch(on?.Id ?? 0, url, url: true));
    }

    /// <summary>A file in the app set for its type, a folder in the file
    /// manager.</summary>
    public static void Path(string path, Window? on = null)
    {
        Application.VerifyAccess();
        Native.Check(Native.Launch(on?.Id ?? 0, path, url: false));
    }
}
