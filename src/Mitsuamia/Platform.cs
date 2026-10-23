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
