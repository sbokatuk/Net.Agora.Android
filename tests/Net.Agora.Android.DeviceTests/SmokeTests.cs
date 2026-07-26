// The whole RTC suite is compiled into the RTC flavors only, rather than branched at runtime: with
// any other product's package referenced, nothing under Agora.Rtc even resolves. Every flavor
// carries a file like this one declaring the same SmokeTests class behind its own define —
// SignalingSmokeTests.cs, ChatSmokeTests.cs, WhiteboardSmokeTests.cs, FastboardSmokeTests.cs — so
// MainActivity needs no flavor awareness at all. AGORA_RTC covers Video and Voice because those
// two share this one suite; see the .csproj for why it is a positive define.
#if AGORA_RTC
using Agora.Rtc;
#endif

namespace Net.Agora.Android.DeviceTests;

/// <summary>A single on-device check. Throws to fail.</summary>
public sealed record SmokeTest(string Name, Action Execute);

#if AGORA_RTC
/// <summary>
/// End-to-end checks that only mean anything on a real device or emulator: they load the native
/// Agora engine out of the packaged .aar files and drive the raw binding —
/// <see cref="RtcEngine"/> itself, no cross-platform façade in between. The façade's own on-device
/// suite lives in the Net.Agora repository; this one exists to say whether *this package* works,
/// so a failure there can be attributed to the right repository.
/// </summary>
/// <remarks>
/// Nothing here needs real credentials. Joining a channel is a live signalling call to Agora's
/// servers, so these checks stop short of it: the App ID is syntactically valid (32 lowercase hex,
/// the shape the SDK's client-side validation checks) but unregistered, which is enough to create
/// the engine, load the native libraries and drive the local-only surface — the parts that prove
/// the packaging and the JNI wiring, which is what this suite is for. No <c>AGORA_APP_ID</c>
/// secret is required and nothing would leak if one were passed in.
/// <para>
/// The checks are ordered: the engine has to be created before it can be driven, and driven before
/// it can be destroyed. A failure early on therefore cascades, which is the intent — the first
/// failure is the informative one.
/// </para>
/// </remarks>
public static class SmokeTests
{
    // 32 lowercase hex characters — the shape of a real Agora App ID — so the SDK's own format
    // validation (client-side, no network needed) does not reject engine creation before the
    // checks that actually exercise it.
    private const string AppId = "0123456789abcdef0123456789abcdef";

    public static Action<string> Reporter { get; set; } = _ => { };

    private static void Report(string message) => Reporter(message);

    private static Context Context => global::Android.App.Application.Context;

    /// <summary>The engine every check after creation shares. Set by <see cref="CreatesTheEngine"/>.</summary>
    private static RtcEngine? _engine;

    private static RtcEngine Engine =>
        _engine ?? throw new InvalidOperationException("the engine has not been created yet.");

    // The AGORA_VOICE build (see the .csproj) compiles the video checks out entirely rather than
    // branching at runtime: voice-rtc-basic ships the same Java API layer with the video pipeline
    // stripped from the native library, so the video calls still *compile* against the voice
    // binding — they just answer error codes that vary by call. Gating at build time keeps every
    // remaining assertion strict.
    public static SmokeTest[] All =>
    [
        new("the Java entry points resolve from the packaged .aar", JavaEntryPointsResolve),
        new("reports the native SDK version", ReportsTheSdkVersion),
        new("answers an error description without an engine", AnswersAnErrorDescription),
        new("creates the engine from a syntactically valid App ID", CreatesTheEngine),
        new("subscribes and unsubscribes the binding's C# events", SubscribesTheCSharpEvents),
#if AGORA_VOICE
        new("enables and disables audio", EnablesAndDisablesMedia),
        new("mutes and unmutes the local audio stream", MutesAndUnmutesLocalStreams),
#else
        new("enables and disables video and audio", EnablesAndDisablesMedia),
        new("mutes and unmutes the local streams", MutesAndUnmutesLocalStreams),
        new("starts and stops the local preview", StartsAndStopsPreview),
#endif
        new("destroys the engine", DestroysTheEngine),
    ];

    /// <summary>
    /// Proves the .aar files actually made it into the app.
    /// </summary>
    /// <remarks>
    /// This is the check that catches a packaging regression the compiler cannot see. A binding
    /// assembly reaches its Java classes through JNI lookups by name, so a package whose .aar was
    /// missing still compiles and links - and then throws ClassNotFoundException at runtime the
    /// first time a type is touched. That is exactly the failure mode @(AndroidMavenLibrary) being
    /// silently ignored on net8 would produce — see src/Agora.Binding.props.
    /// <para>
    /// The Java names are io.agora.rtc2.*: @(AndroidNamespaceReplacement) renames the *C#*
    /// namespace to Agora.Rtc, but the classes inside the .aar keep their own package.
    /// </para>
    /// </remarks>
    private static void JavaEntryPointsResolve()
    {
        string[] classes =
        [
            "io.agora.rtc2.RtcEngine",
            "io.agora.rtc2.RtcEngineConfig",
            "io.agora.rtc2.IRtcEngineEventHandler",
            "io.agora.rtc2.ChannelMediaOptions",
            "io.agora.rtc2.video.VideoCanvas",
        ];

        // The app's own class loader, not Class.forName(String). The single-argument overload
        // resolves against the *caller's* loader, and the caller here is a runtime frame whose
        // loader is the boot classpath - so every application class comes back not-found, however
        // correctly it was packaged.
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
        // Static, so it works before any engine exists — the first call that crosses into Agora's
        // Java code rather than merely resolving it.
        var version = RtcEngine.SdkVersion;

        Assert(!string.IsNullOrWhiteSpace(version), "RtcEngine.SdkVersion was null or empty.");
        Report($"native SDK version {version}");
    }

    private static void AnswersAnErrorDescription()
    {
        // 101 is ERR_INVALID_APP_ID — a code this suite could plausibly produce for real.
        var description = RtcEngine.GetErrorDescription(101);

        Assert(
            !string.IsNullOrWhiteSpace(description),
            "RtcEngine.GetErrorDescription(101) was null or empty.");
        Report($"error 101: {description}");
    }

    /// <summary>
    /// The check that proves the JNI wiring end to end: creating the engine is what loads
    /// libagora-rtc-sdk.so (and aosl, its one native dependency — shipped Bind="false", so
    /// nothing before this moment would notice it missing).
    /// </summary>
    private static void CreatesTheEngine()
    {
        // RtcEngineConfig binds each of its Java fields (mAppId, mContext, ...) twice: once as the
        // plain field (MAppId, settable) and once through a same-named read-only getter method
        // (AppId) the SDK also exposes. Only the M-prefixed field form has a setter.
        //
        // No MEventHandler: create() only requires the context (addHandler(null) is a no-op in
        // the Java SDK — verified against the 4.6.3 bytecode), and the next check subscribes the
        // binding's C# events instead, which install their own handler through AddHandler.
        var config = new RtcEngineConfig
        {
            MContext = Context,
            MAppId = AppId,
        };

        _engine = RtcEngine.Create(config);

        Assert(_engine is not null, "RtcEngine.Create returned null.");
        Report("engine created — native libraries loaded");
    }

    /// <summary>
    /// Exercises the hand-written events surface (Additions/RtcEngineEvents.cs in the binding)
    /// on a real engine: the first subscription constructs the private dispatcher — a Java
    /// callable wrapper, so this proves its ACW is packaged and registered — and crosses JNI
    /// through AddHandler. Nothing here can make a callback *fire* without joining a channel
    /// (a live signalling call this suite deliberately stops short of), so subscribe/unsubscribe
    /// completing without a throw is the assertion.
    /// </summary>
    private static void SubscribesTheCSharpEvents()
    {
        EventHandler<RtcErrorEventArgs> onError = (_, e) => Report($"error event: {e.Code}");
        EventHandler<RtcJoinChannelSuccessEventArgs> onJoined = (_, e) => Report($"joined {e.Channel}");

        Engine.Error += onError;
        Engine.JoinChannelSuccess += onJoined;
        Engine.JoinChannelSuccess -= onJoined;
        Engine.Error -= onError;

        Report("C# events subscribed through the additions dispatcher");
    }

    private static void EnablesAndDisablesMedia()
    {
        // None of these join anything; they flip local engine state and return an Agora error
        // code, 0 for success. The runner grants camera/microphone permission at install time so
        // a permission failure cannot masquerade as a binding one.
#if !AGORA_VOICE
        AssertOk(Engine.EnableVideo(), "EnableVideo");
        AssertOk(Engine.DisableVideo(), "DisableVideo");
        AssertOk(Engine.EnableVideo(), "EnableVideo (again)");
#endif
        AssertOk(Engine.EnableAudio(), "EnableAudio");
        AssertOk(Engine.DisableAudio(), "DisableAudio");
    }

    private static void MutesAndUnmutesLocalStreams()
    {
        AssertOk(Engine.MuteLocalAudioStream(true), "MuteLocalAudioStream(true)");
        AssertOk(Engine.MuteLocalAudioStream(false), "MuteLocalAudioStream(false)");
#if !AGORA_VOICE
        AssertOk(Engine.MuteLocalVideoStream(true), "MuteLocalVideoStream(true)");
        AssertOk(Engine.MuteLocalVideoStream(false), "MuteLocalVideoStream(false)");
#endif
    }

#if !AGORA_VOICE
    private static void StartsAndStopsPreview()
    {
        // The preview is local-only — no channel, no server, but it does spin the camera pipeline
        // up and down, which is the closest this suite gets to real video without credentials.
        AssertOk(Engine.StartPreview(), "StartPreview");
        AssertOk(Engine.StopPreview(), "StopPreview");
    }
#endif

    private static void DestroysTheEngine()
    {
        // Static, like Create: the engine is a process-wide singleton. Destroy blocks until the
        // native side has torn down, so returning at all is the assertion.
        RtcEngine.Destroy();
        _engine = null;

        Report("engine destroyed");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertOk(int result, string call)
    {
        if (result != 0)
        {
            throw new InvalidOperationException(
                $"{call} returned {result}: {RtcEngine.GetErrorDescription(-result)}");
        }
    }
}
#endif
