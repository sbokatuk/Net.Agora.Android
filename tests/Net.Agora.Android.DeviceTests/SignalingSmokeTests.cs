// The Signaling flavor's suite — see SmokeTests.cs for why each flavor carries a whole class of
// the same name behind a define instead of branching at runtime: with only
// Net.Agora.Signaling.Android referenced, nothing under Agora.Rtc resolves, and MainActivity
// stays flavor-blind.
#if AGORA_SIGNALING
using Agora.Rtm;

namespace Net.Agora.Android.DeviceTests;

/// <summary>
/// End-to-end checks for the packaged Signaling (RTM) binding: they load the native RTM engine
/// out of the packaged .aar and drive the raw binding — <see cref="RtmClient"/> plus the
/// hand-written Task adapters (<c>LoginAsync</c> and friends) that Additions/ ships in the
/// package, so this suite exercises the async path a consumer actually uses.
/// </summary>
/// <remarks>
/// Nothing here needs real credentials. Logging in is a live signalling exchange that would need
/// a registered App ID, so these checks arrange to be *answered* rather than accepted: publishing
/// before login is rejected client-side with error code -10025 (<c>NOT_LOGIN</c> — the same code
/// this project has verified on the iOS and macOS bindings, so it doubles as a cross-platform
/// consistency check), and a login with a garbage token must fault rather than hang. Both arrive
/// through <see cref="RtmOperationException"/>, which is the point: the async adapters' fault
/// path is packaged code, and this is where it runs on a device.
/// <para>
/// The checks are ordered: the client has to be created before it can be driven, and driven
/// before it is released. A failure early on therefore cascades, which is the intent — the first
/// failure is the informative one.
/// </para>
/// </remarks>
public static class SmokeTests
{
    // 32 lowercase hex characters — the shape of a real Agora App ID — so the SDK's client-side
    // format validation does not reject client creation before the checks that exercise it.
    private const string AppId = "0123456789abcdef0123456789abcdef";

    /// <summary>
    /// Generous ceiling for one ResultCallback round trip. The checks below are answered
    /// client-side (no server round trip is required to refuse them), so hitting this means the
    /// callback machinery — not the network — is broken.
    /// </summary>
    private static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(60);

    public static Action<string> Reporter { get; set; } = _ => { };

    private static void Report(string message) => Reporter(message);

    private static Context Context => global::Android.App.Application.Context;

    /// <summary>The client every check after creation shares. Set by <see cref="CreatesTheClient"/>.</summary>
    private static RtmClient? _client;

    private static RtmClient Client =>
        _client ?? throw new InvalidOperationException("the client has not been created yet.");

    public static SmokeTest[] All =>
    [
        new("the Java entry points resolve from the packaged .aar", JavaEntryPointsResolve),
        new("reports the native SDK version", ReportsTheSdkVersion),
        new("creates the client from a syntactically valid App ID", CreatesTheClient),
        new("publish before login faults with NOT_LOGIN through PublishAsync", PublishBeforeLoginFaultsWithNotLogin),
        new("login with a garbage token faults rather than hangs", LoginWithAGarbageTokenFaults),
        new("releases the client", ReleasesTheClient),
    ];

    /// <summary>
    /// Proves the .aar actually made it into the app — same reasoning as the RTC flavor's check
    /// (see SmokeTests.cs), plus one more on the shrink leg: this flavor is the e2e matrix's R8
    /// leg, and agora-rtm ships no proguard.txt of its own, so these classes surviving shrinking
    /// is precisely what the package's packed proguard/ rules exist to guarantee.
    /// </summary>
    private static void JavaEntryPointsResolve()
    {
        string[] classes =
        [
            "io.agora.rtm.RtmClient",
            "io.agora.rtm.RtmConfig",
            "io.agora.rtm.ResultCallback",
            "io.agora.rtm.ErrorInfo",
            "io.agora.rtm.PublishOptions",
        ];

        // The app's own class loader, not Class.forName(String) — see the RTC flavor for why the
        // single-argument overload resolves against the wrong loader here.
        var loader = Context.ClassLoader!;

        var missing = new List<string>();
        foreach (var name in classes)
        {
            try
            {
                _ = Java.Lang.Class.ForName(name, false, loader);
            }
            catch (Java.Lang.ClassNotFoundException)
            {
                missing.Add(name);
            }
        }

        Assert(missing.Count == 0, $"these Java classes are not in the app: {string.Join(", ", missing)}");
        Report($"all {classes.Length} Java entry points resolved");
    }

    private static void ReportsTheSdkVersion()
    {
        // Static, so it works before any client exists — and it is the JNI wiring check too:
        // getVersion() loads the native library and answers an empty string if that fails.
        var version = RtmClient.Version;

        Assert(!string.IsNullOrWhiteSpace(version), "RtmClient.Version was null or empty.");
        Report($"native SDK version {version}");
    }

    private static void CreatesTheClient()
    {
        // No .EventListener(...): the builder accepts a config without one (the generated
        // RtmClient exposes the callbacks as C# events instead), which this check proves on
        // device. Create throws IllegalArgumentException — a Java.Lang.Exception — when the SDK
        // rejects the configuration, so reaching the assert at all is most of the check.
        var config = new RtmConfig.Builder(AppId, "net-agora-devicetests").Build();

        _client = RtmClient.Create(config);

        Assert(_client is not null, "RtmClient.Create returned null.");
        Report("client created — native libraries loaded");
    }

    /// <summary>
    /// The async adapters' fault path, end to end on a device: PublishAsync before any login is
    /// refused client-side with -10025 (<c>NOT_LOGIN</c>), so the Task must fault with
    /// <see cref="RtmOperationException"/> carrying that code — no server, no credentials.
    /// </summary>
    private static void PublishBeforeLoginFaultsWithNotLogin()
    {
        var exception = AwaitFault(
            Client.PublishAsync("devicetests-channel", "hello before login"),
            "PublishAsync before login");

        var failure = exception as RtmOperationException
            ?? throw new InvalidOperationException(
                $"expected RtmOperationException, got {exception.GetType().Name}: {exception.Message}");

        Assert(failure.ErrorInfo is not null, "the exception carried no ErrorInfo.");
        var code = RtmConstants.RtmErrorCode.GetValue(failure.ErrorInfo!.ErrorCode);
        Assert(code == -10025, $"expected error code -10025 (NOT_LOGIN), got {code}: {failure.Message}");

        Report($"publish before login answered {code} ({failure.ErrorInfo.ErrorReason})");
    }

    /// <summary>
    /// "Faults rather than hangs": a garbage token must surface as a faulted Task inside the
    /// bounded wait, whatever the exact code — the SDK may refuse it client-side or after a
    /// round trip, and either way the only wrong outcomes are success and silence.
    /// </summary>
    private static void LoginWithAGarbageTokenFaults()
    {
        var exception = AwaitFault(
            Client.LoginAsync("not-a-token"),
            "LoginAsync with a garbage token");

        Assert(
            exception is RtmOperationException,
            $"expected RtmOperationException, got {exception.GetType().Name}: {exception.Message}");

        Report($"login faulted as expected: {exception.Message}");
    }

    private static void ReleasesTheClient()
    {
        // Static, like Create: the client is a process-wide singleton. Returning at all is the
        // assertion.
        RtmClient.Release();
        _client = null;

        Report("client released");
    }

    /// <summary>
    /// Waits (bounded) for a Task that is *expected* to fault and hands back its exception. The
    /// bound exists because a hang is one of the failure modes these checks assert against — it
    /// must become a named failure, not the runner's generic timeout.
    /// </summary>
    private static Exception AwaitFault(Task operation, string call)
    {
        // Synchronous rather than async: SmokeTest.Execute is an Action, and MainActivity
        // already runs the suite off the UI thread, so blocking here deadlocks nothing.
        var winner = Task.WhenAny(operation, Task.Delay(CallbackTimeout)).GetAwaiter().GetResult();

        if (winner != operation)
        {
            throw new InvalidOperationException(
                $"{call} neither completed nor faulted within {CallbackTimeout.TotalSeconds:0}s — the ResultCallback never fired.");
        }

        if (!operation.IsFaulted)
        {
            throw new InvalidOperationException($"{call} unexpectedly succeeded.");
        }

        return operation.Exception!.InnerException ?? operation.Exception!;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
