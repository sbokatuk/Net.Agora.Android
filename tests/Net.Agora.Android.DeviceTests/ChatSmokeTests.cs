// The Chat flavor's suite — see SmokeTests.cs for why each flavor carries a whole class of the
// same name behind a define instead of branching at runtime: with only Net.Agora.Chat.Android
// referenced, nothing under Agora.Rtc or Agora.Rtm resolves, and MainActivity stays flavor-blind.
#if AGORA_CHAT
using System.IO.Compression;

using Agora.Chat;

namespace Net.Agora.Android.DeviceTests;

/// <summary>
/// End-to-end checks for the packaged Chat (IM) binding: they load the native Chat engine out of
/// the packaged .aar and drive the raw binding — <see cref="ChatClient"/>, its options object and
/// its manager surface — with no cross-platform façade in between. The façade's own on-device
/// suite lives in the Net.Agora repository; this one exists to say whether *this package* works,
/// so a failure there can be attributed to the right repository.
/// </summary>
/// <remarks>
/// Nothing here needs real credentials. Signing in is a live exchange with Agora's servers that
/// would need a registered App ID, so these checks stop short of it: the App ID is syntactically
/// valid (32 lowercase hex, the shape the SDK's client-side validation checks) but unregistered,
/// which is enough to initialise the SDK, load the native libraries and drive the local-only
/// surface — the parts that prove the packaging and the JNI wiring, which is what this suite is
/// for. The one call that does cross into the SDK's own refusal path (a send before any sign-in)
/// is answered client-side and is bounded, so no check can hang.
/// <para>
/// "Needs no network" is meant literally and was checked that way: the suite passes 10/10 on an
/// emulator with wifi and mobile data switched off. Worth pinning, because Chat is the one product
/// here whose initialisation could plausibly have reached for a server.
/// </para>
/// <para>
/// The checks are ordered: the SDK has to be initialised before it can be driven, and driven
/// before it is signed out. A failure early on therefore cascades, which is the intent — the first
/// failure is the informative one.
/// </para>
/// </remarks>
public static class SmokeTests
{
    // 32 lowercase hex characters — the shape of a real Agora App ID — so the SDK's client-side
    // format validation does not reject initialisation before the checks that exercise it.
    private const string AppId = "0123456789abcdef0123456789abcdef";

    /// <summary>
    /// Chat's *other* credential shape. It is the rebranded Easemob/Hyphenate IM SDK, so it still
    /// accepts an <c>orgName#appName</c> app key instead of an Agora App ID — the two are
    /// alternatives, never both. This one is well-formed and belongs to nobody; only
    /// <see cref="ConstructsOptionsFromAnAppKey"/> uses it, and only to construct.
    /// </summary>
    private const string AppKey = "netagora#devicetests";

    /// <summary>
    /// Generous ceiling for one CallBack round trip. The check that waits on one is refused
    /// client-side (no server round trip is required to refuse it), so hitting this means the
    /// callback machinery — not the network — is broken.
    /// </summary>
    private static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(60);

    public static Action<string> Reporter { get; set; } = _ => { };

    private static void Report(string message) => Reporter(message);

    private static Context Context => global::Android.App.Application.Context;

    /// <summary>The client every check after <see cref="ObtainsTheClientSingleton"/> shares.</summary>
    private static ChatClient? _client;

    private static ChatClient Client =>
        _client ?? throw new InvalidOperationException("the client has not been obtained yet.");

    private static ChatManager? _chatManager;

    private static ChatManager ChatManager =>
        _chatManager ?? throw new InvalidOperationException("the SDK has not been initialised yet.");

    public static SmokeTest[] All =>
    [
        new("the Java entry points resolve from the packaged .aar", JavaEntryPointsResolve),
        new("the native libraries are in the app", NativeLibrariesAreInTheApp),
        new("reports the SDK version", ReportsTheSdkVersion),
        new("constructs options from an unregistered app key", ConstructsOptionsFromAnAppKey),
        new("obtains the client singleton", ObtainsTheClientSingleton),
        new("initialises the SDK from a syntactically valid App ID", InitialisesTheSdk),
        new("reads the local state of a signed-out client", ReadsTheSignedOutState),
        new("subscribes and unsubscribes the binding's C# events", SubscribesTheCSharpEvents),
        new("a send before sign-in is refused through the SDK's callback", SendBeforeSignInIsRefused),
        new("signs out without a session rather than hanging", SignsOutWithoutASession),
    ];

    /// <summary>
    /// Proves the .aar actually made it into the app — same reasoning as the RTC flavor's check
    /// (see SmokeTests.cs): a binding assembly reaches its Java classes through JNI lookups by
    /// name, so a package whose .aar was missing still compiles and links, then throws
    /// ClassNotFoundException the first time a type is touched.
    /// </summary>
    /// <remarks>
    /// Two Java packages on purpose. io.agora.chat is the product's own, renamed to Agora.Chat by
    /// @(AndroidNamespaceReplacement); io.agora — bare — is where the SDK keeps its callback
    /// interfaces (CallBack, ConnectionListener), which the rename does not touch and which the
    /// checks below reach through the generated implementors.
    /// </remarks>
    private static void JavaEntryPointsResolve()
    {
        string[] classes =
        [
            "io.agora.chat.ChatClient",
            "io.agora.chat.ChatOptions",
            "io.agora.chat.ChatManager",
            "io.agora.chat.ChatMessage",
            "io.agora.CallBack",
            "io.agora.ConnectionListener",
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

    /// <summary>
    /// The other half of the payload, and the half Class.forName cannot see. Chat is the one
    /// non-RTC product here that ships native code — libagora-chat-sdk.so, its libcipherdb.so
    /// database layer and libaosl.so, all three inside the .aar's own jni/ directory rather than
    /// pulled from a separate infra artifact — and a build that dropped them would still resolve
    /// every Java class and then die inside <see cref="InitialisesTheSdk"/> with an
    /// UnsatisfiedLinkError. Checking first means the failure names the missing file.
    /// </summary>
    private static void NativeLibrariesAreInTheApp()
    {
        string[] libraries = ["libagora-chat-sdk.so", "libcipherdb.so", "libaosl.so"];

        var applicationInfo = Context.ApplicationInfo
            ?? throw new InvalidOperationException("Context.ApplicationInfo was null.");

        // Where the .so files live is the platform installer's decision, not ours, so both places
        // count: extracted into nativeLibraryDir, or left inside the APK to be mapped straight out
        // of it (android:extractNativeLibs="false", which is the default for a modern build).
        var extracted = new HashSet<string>(StringComparer.Ordinal);
        var nativeLibraryDir = applicationInfo.NativeLibraryDir;
        if (!string.IsNullOrEmpty(nativeLibraryDir) && Directory.Exists(nativeLibraryDir))
        {
            foreach (var file in Directory.GetFiles(nativeLibraryDir))
            {
                extracted.Add(Path.GetFileName(file));
            }
        }

        var missing = libraries.Where(library => !extracted.Contains(library)).ToList();
        var where = "the native library directory";

        if (missing.Count > 0)
        {
            using var apk = ZipFile.OpenRead(applicationInfo.SourceDir!);
            var packaged = new HashSet<string>(
                apk.Entries
                    .Where(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal))
                    .Select(entry => entry.Name),
                StringComparer.Ordinal);

            missing = missing.Where(library => !packaged.Contains(library)).ToList();
            where = extracted.Count == 0 ? "the APK" : "the native library directory and the APK";
        }

        Assert(missing.Count == 0, $"these native libraries are not in the app: {string.Join(", ", missing)}");
        Report($"all {libraries.Length} native libraries found in {where}");
    }

    private static void ReportsTheSdkVersion()
    {
        // A compile-time constant in the Java SDK rather than a native call, so it says nothing
        // about the .so files — it is the classes.jar the binding was generated from answering,
        // which is worth having on its own: a package built against one native version and
        // shipping another shows up here.
        var version = ChatClient.Version;

        Assert(!string.IsNullOrWhiteSpace(version), "ChatClient.Version was null or empty.");
        Report($"SDK version {version}");
    }

    /// <summary>
    /// Construction only, and deliberately so: the app key is the alternative to the App ID, and
    /// the SDK takes exactly one of the two. Initialising with this one would make every later
    /// check speak to a different (still unregistered) identity for no gain, so this proves the
    /// property round-trips through JNI and stops there.
    /// </summary>
    private static void ConstructsOptionsFromAnAppKey()
    {
        var options = new ChatOptions { AppKey = AppKey };

        Assert(options.AppKey == AppKey, $"ChatOptions.AppKey answered '{options.AppKey}'.");
        Report($"options accepted the unregistered app key {AppKey}");
    }

    private static void ObtainsTheClientSingleton()
    {
        // getInstance() constructs the singleton on first call, so this is the first crossing into
        // Agora's Java code — but not yet into its native code, which Init below is what loads.
        _client = ChatClient.Instance;

        Assert(_client is not null, "ChatClient.Instance returned null.");
        Assert(!Client.IsSdkInited, "the SDK reports itself initialised before Init was called.");
        Report("client singleton obtained");
    }

    /// <summary>
    /// The check that proves the JNI wiring end to end: init() is what loads
    /// libagora-chat-sdk.so and opens the SDK's local database, and it is safe offline — it signs
    /// nothing in and opens no session of its own.
    /// </summary>
    private static void InitialisesTheSdk()
    {
        // AutoLogin off explicitly. It defaults to on, and on a device that had signed in before
        // that would have init() resume the session — a live call, from a check that is meant to
        // need no network. A fresh install has nothing to resume, but the run is only offline-safe
        // by accident then, and this suite should not depend on the emulator being fresh.
        var options = new ChatOptions
        {
            AppId = AppId,
            AutoLogin = false,
        };

        // ApplicationContext, not the Activity: the singleton outlives any Activity, and the SDK
        // holds whatever it is given for the life of the process.
        Client.Init(Context.ApplicationContext ?? Context, options);

        Assert(Client.IsSdkInited, "ChatClient.IsSdkInited is false after Init.");

        _chatManager = Client.ChatManager();
        Assert(_chatManager is not null, "ChatClient.ChatManager() returned null after Init.");

        Report("SDK initialised — native libraries loaded");
    }

    private static void ReadsTheSignedOutState()
    {
        Assert(!Client.IsLoggedIn, "IsLoggedIn is true before any sign-in was attempted.");
        Assert(
            string.IsNullOrEmpty(Client.CurrentUser),
            $"CurrentUser is '{Client.CurrentUser}' before any sign-in was attempted.");

        // Local-only: it answers out of the SDK's own database without a session, which on a fresh
        // install is empty. What is proved is that the manager surface is reachable and that the
        // generated property (Java's getAllConversationsBySort()) marshals a Java List back as an
        // ordinary IList rather than null or a throw.
        var conversations = ChatManager.AllConversationsBySort;

        Assert(conversations is not null, "ChatManager.AllConversationsBySort returned null.");
        Report($"signed out, {conversations!.Count} conversation(s) in the local database");
    }

    /// <summary>
    /// Exercises the generated events surface on the real singleton: the first subscription
    /// constructs the implementor — a Java callable wrapper, so this proves its ACW is packaged
    /// and registered — and crosses JNI through addConnectionListener. Nothing here can make a
    /// callback *fire* without a session, so subscribe/unsubscribe completing without a throw is
    /// the assertion.
    /// </summary>
    private static void SubscribesTheCSharpEvents()
    {
        // One of each shape: Connected carries no arguments, Disconnected carries generated
        // EventArgs, and they are raised from the same Java listener — so a break in either the
        // plain or the generic half shows up here.
        EventHandler onConnected = (_, _) => Report("connected");
        EventHandler<IO.Agora.DisconnectedEventArgs> onDisconnected = (_, _) => Report("disconnected");

        Client.Connected += onConnected;
        Client.Disconnected += onDisconnected;
        Client.Disconnected -= onDisconnected;
        Client.Connected -= onConnected;

        Report("C# events subscribed through the generated listener implementor");
    }

    /// <summary>
    /// The SDK's own refusal path, end to end on a device: a send with no session is rejected
    /// client-side, and the rejection arrives through the CallBack the message carries — the
    /// completion shape Chat uses on Android, where iOS takes a block on the send call. No server,
    /// no credentials, and bounded, so a callback that never fires is a named failure rather than
    /// the runner's generic timeout.
    /// </summary>
    private static void SendBeforeSignInIsRefused()
    {
        var message = ChatMessage.CreateTextSendMessage("net-agora-devicetests-peer", "hello before sign-in")
            ?? throw new InvalidOperationException("ChatMessage.CreateTextSendMessage returned null.");

        var answered = new TaskCompletionSource<(int Code, string? Description)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        message.SetMessageStatusCallback(new Callback(
            () => answered.TrySetResult((0, null)),
            (code, description) => answered.TrySetResult((code, description))));

        ChatManager.SendMessage(message);

        var result = Await(answered.Task, "the send callback");

        Assert(result.Code != 0, "a send before any sign-in reported success.");
        Report($"send refused: [{result.Code}] {result.Description}");
    }

    /// <summary>
    /// The nearest thing Chat has to a teardown: there is no de-init, and the singleton lives as
    /// long as the process, so signing out is where the SDK releases what a session holds. With no
    /// session to release it must still answer — succeed or fail, either is fine — rather than
    /// wait on a server, which is the property this bounded wait pins.
    /// </summary>
    private static void SignsOutWithoutASession()
    {
        var answered = new TaskCompletionSource<(int Code, string? Description)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // false: do not unbind the device token, which would be a push-service call.
        Client.Logout(false, new Callback(
            () => answered.TrySetResult((0, null)),
            (code, description) => answered.TrySetResult((code, description))));

        var result = Await(answered.Task, "the sign-out callback");

        Assert(!Client.IsLoggedIn, "IsLoggedIn is true after signing out.");
        Report(result.Code == 0
            ? "signed out"
            : $"sign-out answered [{result.Code}] {result.Description} — no session to end");
    }

    /// <summary>
    /// Waits (bounded) for a callback-backed Task and hands back its result. The bound exists
    /// because a hang is one of the failure modes these checks assert against — it must become a
    /// named failure, not the runner's generic timeout.
    /// </summary>
    private static T Await<T>(Task<T> operation, string what)
    {
        // Synchronous rather than async: SmokeTest.Execute is an Action, and MainActivity already
        // runs the suite off the UI thread, so blocking here deadlocks nothing.
        var winner = Task.WhenAny(operation, Task.Delay(CallbackTimeout)).GetAwaiter().GetResult();

        if (winner != operation)
        {
            throw new InvalidOperationException(
                $"{what} never fired within {CallbackTimeout.TotalSeconds:0}s.");
        }

        return operation.GetAwaiter().GetResult();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// The SDK's universal completion interface. Implementing it in C# is what makes the build
    /// emit an Android callable wrapper for it, so every check that waits on one is also checking
    /// that the ACW was packaged and registered.
    /// </summary>
    private sealed class Callback(Action succeeded, Action<int, string?> failed)
        : Java.Lang.Object, IO.Agora.ICallBack
    {
        public void OnSuccess() => succeeded();

        public void OnError(int code, string? description) => failed(code, description);

        // Progress is only reported for attachment transfers; there is nothing to do with it here,
        // but the interface requires it.
        public void OnProgress(int progress, string? status)
        {
        }
    }
}
#endif
