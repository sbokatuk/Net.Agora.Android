// Only the two board flavors need this. Whiteboard and Fastboard are the only products here whose
// entry point is a View — a WebView subclass in both cases — and Android requires a WebView to be
// created and touched on the thread that owns its Looper, which in an app is the main one. Every
// other flavor's SDK is a plain object and runs wherever the suite runs.
#if AGORA_WHITEBOARD || AGORA_FASTBOARD
namespace Net.Agora.Android.DeviceTests;

/// <summary>
/// Runs a piece of work on the UI thread and waits (bounded) for its result, rethrowing whatever
/// it threw on the caller's thread so a failure still lands in the check that caused it.
/// </summary>
/// <remarks>
/// The bound matters as much as the marshalling: a check that posts work to a main thread which is
/// wedged would otherwise hang until the runner script's own timeout, which reports "no verdict"
/// and names nothing. Here it becomes a named failure attributed to one check.
/// </remarks>
internal static class UiThread
{
    /// <summary>
    /// Generous ceiling for one posted piece of work. Constructing a WebView is the slowest thing
    /// these suites ask of the main thread — on a cold emulator that includes loading the WebView
    /// provider itself — so this is sized for that rather than for the work's own cost.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    internal static T Run<T>(string what, Func<T> work)
    {
        var main = Looper.MainLooper
            ?? throw new InvalidOperationException("Looper.MainLooper was null.");

        // MainActivity runs the suite off the UI thread (see its comment), which is what makes
        // blocking here safe. Said out loud rather than assumed, because if that ever changes this
        // would deadlock silently and every board check would report the timeout above.
        if (Looper.MyLooper() == main)
        {
            throw new InvalidOperationException(
                $"{what} was posted to the UI thread from the UI thread, which would deadlock.");
        }

        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        new Handler(main).Post(() =>
        {
            try
            {
                done.TrySetResult(work());
            }
            catch (Exception exception)
            {
                done.TrySetException(exception);
            }
        });

        var winner = Task.WhenAny(done.Task, Task.Delay(Timeout)).GetAwaiter().GetResult();

        if (winner != done.Task)
        {
            throw new InvalidOperationException(
                $"{what} did not complete on the UI thread within {Timeout.TotalSeconds:0}s.");
        }

        // GetAwaiter().GetResult() rather than .Result: it rethrows the original exception rather
        // than an AggregateException wrapping it, so the logged failure names the real cause.
        return done.Task.GetAwaiter().GetResult();
    }

    internal static void Run(string what, Action work) =>
        Run(what, () =>
        {
            work();
            return true;
        });
}
#endif
