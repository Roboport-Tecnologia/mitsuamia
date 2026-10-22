using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Mitsuamia;

/// <summary>
/// Runs the app on mitsuami. Call <see cref="Run"/> from <c>Main</c>: it
/// takes the main thread, which AppKit needs, until the last window closes.
/// </summary>
public static unsafe class Application
{
    private static Action? _setup;
    private static Thread? _uiThread;

    /// <summary>Exceptions thrown in event handlers and posted work. They
    /// can't unwind into mitsuami; without a handler they're written to
    /// standard error.</summary>
    public static event Action<Exception>? UnhandledException;

    public static bool IsUiThread => Thread.CurrentThread == _uiThread;

    /// <summary>Whether <see cref="Run"/> has started the run loop, so
    /// <see cref="Post"/> has somewhere to post to.</summary>
    public static bool IsRunning => _uiThread is not null;

    public static void Run(string id, string name, Action setup)
    {
        // WinUI needs a single-threaded apartment, and .NET starts Main in
        // a multithreaded one unless it's marked [STAThread], which
        // top-level statements can't be. Windows doesn't need the main
        // thread, so the run loop gets a thread of its own.
        if (OperatingSystem.IsWindows() && Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try { Run(id, name, setup); }
                catch (Exception e) { error = e; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error is not null) throw error;
            return;
        }
        _setup = setup;
        var idBytes = Encoding.UTF8.GetBytes(id);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        fixed (byte* i = idBytes)
        fixed (byte* n = nameBytes)
        {
            Native.Check(Native.Run(i, (nuint)idBytes.Length, n, (nuint)nameBytes.Length, &Setup, 0));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Setup(nint user)
    {
        _uiThread = Thread.CurrentThread;
        // So `await` in handlers comes back to the UI thread.
        SynchronizationContext.SetSynchronizationContext(new UiSynchronizationContext());
        Guard(() => _setup!());
    }

    /// <summary>Runs <paramref name="action"/> on the UI thread soon. Any
    /// thread may call it.</summary>
    public static void Post(Action action)
    {
        var handle = GCHandle.Alloc(action);
        Native.Post(&Posted, GCHandle.ToIntPtr(handle));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Posted(nint user)
    {
        var handle = GCHandle.FromIntPtr(user);
        var action = (Action)handle.Target!;
        handle.Free();
        Guard(action);
    }

    internal static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            if (UnhandledException is { } handler) handler(e);
            else Console.Error.WriteLine($"mitsuamia: unhandled exception: {e}");
        }
    }

    internal static void VerifyAccess()
    {
        if (!IsUiThread)
            throw new InvalidOperationException("mitsuamia: UI objects are used on the UI thread only (Application.Post)");
    }
}

/// <summary>Posts to mitsuami's run loop.</summary>
public sealed class UiSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state) => Application.Post(() => d(state));

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (Application.IsUiThread)
        {
            d(state);
            return;
        }
        using var done = new ManualResetEventSlim();
        Exception? error = null;
        Application.Post(() =>
        {
            try { d(state); }
            catch (Exception e) { error = e; }
            finally { done.Set(); }
        });
        done.Wait();
        if (error is not null) throw error;
    }

    public override SynchronizationContext CreateCopy() => this;
}
