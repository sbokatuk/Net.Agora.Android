// The Whiteboard flavor's suite — see SmokeTests.cs for why each flavor carries a whole class of
// the same name behind a define instead of branching at runtime: with only
// Net.Agora.Whiteboard.Android referenced, nothing under Agora.Rtc or Agora.Rtm resolves, and
// MainActivity stays flavor-blind.
#if AGORA_WHITEBOARD
using Agora.Whiteboard;

using Wendu.Dsbridge.Special;

namespace Net.Agora.Android.DeviceTests;

/// <summary>
/// End-to-end checks for the packaged Interactive Whiteboard binding. Unlike every other product
/// here this is a WebView SDK: the .aar carries no native library at all, only Java and a
/// JavaScript bundle it loads into a WebView, and the board itself is a View
/// (<see cref="WhiteboardView"/>) rather than an object. So the packaging questions are different
/// ones — is the JS bundle in the app's assets, and did DSBridge get *bound* rather than merely
/// carried — and this suite asks those.
/// </summary>
/// <remarks>
/// Nothing here needs real credentials and nothing here joins a room. Joining is a live exchange
/// with netless's servers needing a room UUID and room token minted by your own server against the
/// whiteboard REST API; there is no client-side refusal to arrange, so the checks stop at the last
/// point that is answerable offline — the SDK constructed against a live board view, which is what
/// the packaging and the binding actually decide. The App Identifier is well-formed and belongs to
/// nobody.
/// <para>
/// "Needs no network" is meant literally and was checked that way: the suite passes 9/9 on an
/// emulator with wifi and mobile data switched off — the board's WebView is pointed at a
/// file:///android_asset URL, so the bundle checked below is all it needs.
/// </para>
/// <para>
/// The checks are ordered: the view has to exist before the SDK can be bound to it, and both
/// before either is torn down. A failure early on therefore cascades, which is the intent — the
/// first failure is the informative one.
/// </para>
/// </remarks>
public static class SmokeTests
{
    /// <summary>
    /// Shaped like a netless App Identifier (<c>&lt;access key&gt;/&lt;secret&gt;</c>) and
    /// registered to nobody. Nothing below sends it anywhere; it exists so the configuration is
    /// built the way a real one would be.
    /// </summary>
    private const string AppIdentifier = "netagora0123456789ab/devicetests0123456";

    /// <summary>The asset directory the SDK loads its board into a WebView from.</summary>
    private const string BundleDirectory = "whiteboard";

    public static Action<string> Reporter { get; set; } = _ => { };

    private static void Report(string message) => Reporter(message);

    private static Context Context => global::Android.App.Application.Context;

    /// <summary>The board view every check after <see cref="ConstructsTheBoardView"/> shares.</summary>
    private static WhiteboardView? _boardView;

    private static WhiteboardView BoardView =>
        _boardView ?? throw new InvalidOperationException("the board view has not been created yet.");

    private static WhiteSdk? _sdk;

    public static SmokeTest[] All =>
    [
        new("the Java entry points resolve from the packaged .aar", JavaEntryPointsResolve),
        new("the board view really extends DSBridge's DWebView", TheBoardViewExtendsTheDsBridgeWebView),
        new("reports the SDK version", ReportsTheSdkVersion),
        new("the JavaScript bundle ships as an Android asset", TheJavaScriptBundleShipsAsAnAsset),
        new("constructs the board view on the UI thread", ConstructsTheBoardView),
        new("constructs the configuration from an unregistered App Identifier", ConstructsTheConfiguration),
        new("constructs the SDK against the board view", ConstructsTheSdk),
        new("constructs room parameters without joining", ConstructsRoomParameters),
        new("destroys the board view", DestroysTheBoardView),
    ];

    /// <summary>
    /// Proves the .aars actually made it into the app — same reasoning as the RTC flavor's check
    /// (see SmokeTests.cs), over both of the archives this package carries.
    /// </summary>
    /// <remarks>
    /// com.herewhite.sdk is netless's original company name, which the SDK never renamed;
    /// @(AndroidNamespaceReplacement) renames the *C#* namespace to Agora.Whiteboard, but the
    /// classes inside the .aar keep their own package. wendu.dsbridge.special is the JS-bridge
    /// library netless forked, which travels inside this package because no NuGet binding for it
    /// exists — and which is where a packaging regression would show up first, since it is the one
    /// dependency that is not a PackageReference.
    /// </remarks>
    private static void JavaEntryPointsResolve()
    {
        string[] classes =
        [
            "com.herewhite.sdk.WhiteboardView",
            "com.herewhite.sdk.WhiteSdk",
            "com.herewhite.sdk.WhiteSdkConfiguration",
            "com.herewhite.sdk.RoomParams",
            "com.herewhite.sdk.Room",
            "wendu.dsbridge.special.DWebView",
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
    /// The check that exists because of a failure mode nothing else here can see. DSBridge is
    /// bound (Bind="true"), not merely carried, and it has to be: WhiteboardView extends
    /// wendu.dsbridge.special.DWebView, so with DSBridge unbound the generator's class-parse drops
    /// the view — and every method that takes one — leaving a package that still builds and a
    /// WhiteSdk that arrives with no usable constructor.
    /// </summary>
    /// <remarks>
    /// A managed type-identity check rather than a JNI one, because that is where the damage would
    /// be: the Java class is in the app either way (the check above proves that), what would be
    /// missing is the *binding* for it. Asserting the exact base type also pins the shape the
    /// SDK's WebView-ness depends on — DWebView is itself a WebView, which is what makes the
    /// UI-thread rule below apply and what a consumer relies on when putting the board in a layout.
    /// </remarks>
    private static void TheBoardViewExtendsTheDsBridgeWebView()
    {
        var boardView = typeof(WhiteboardView);

        Assert(
            boardView.BaseType == typeof(DWebView),
            $"WhiteboardView extends {boardView.BaseType?.FullName ?? "nothing"}, not DSBridge's DWebView — " +
            "the DSBridge artifact is carried but not bound.");
        Assert(
            typeof(DWebView).BaseType == typeof(global::Android.Webkit.WebView),
            $"DWebView extends {typeof(DWebView).BaseType?.FullName ?? "nothing"}, not android.webkit.WebView.");

        Report("WhiteboardView -> DWebView -> WebView, all three bound");
    }

    private static void ReportsTheSdkVersion()
    {
        // Static, so it works before any view or SDK exists — the first call that crosses into
        // netless's Java code rather than merely resolving it.
        var version = WhiteSdk.Version();

        Assert(!string.IsNullOrWhiteSpace(version), "WhiteSdk.Version() was null or empty.");
        Report($"SDK version {version}");
    }

    /// <summary>
    /// This package's equivalent of the RTC flavor's native-library check. There are no .so files
    /// to look for; what has to be in the app is the JavaScript bundle the board runs, which the
    /// .aar ships under assets/whiteboard/ and the SDK loads by a fixed
    /// <c>file:///android_asset</c> URL.
    /// </summary>
    /// <remarks>
    /// Worth asserting explicitly because the failure is silent: with the assets missing, the view
    /// still constructs, the SDK still binds to it and the board is simply blank forever — no
    /// exception, no log. index.html is checked by name (it is the entry point, and its name is
    /// stable across versions); the bundle's own files are content-hashed, so they are counted
    /// rather than named.
    /// </remarks>
    private static void TheJavaScriptBundleShipsAsAnAsset()
    {
        var assets = Context.Assets ?? throw new InvalidOperationException("Context.Assets was null.");

        var entries = assets.List(BundleDirectory) ?? [];

        Assert(
            entries.Contains("index.html"),
            $"assets/{BundleDirectory}/index.html is not in the app (the directory holds {entries.Length} entries).");

        // Opened, not just listed: an entry can be present and unreadable if it was packaged into
        // the wrong compression bucket.
        using (var stream = assets.Open($"{BundleDirectory}/index.html"))
        {
            Assert(stream.ReadByte() >= 0, $"assets/{BundleDirectory}/index.html is empty.");
        }

        Report($"the board bundle ships as {entries.Length} asset(s) under {BundleDirectory}/");
    }

    /// <summary>
    /// The board view is a WebView subclass, so it is created on the UI thread — Android requires
    /// it, and a consumer putting the board in a layout is on that thread anyway. Constructing it
    /// is what loads the WebView provider, wires DSBridge's JavaScript interface and points the
    /// view at the asset bundle checked above.
    /// </summary>
    private static void ConstructsTheBoardView()
    {
        _boardView = UiThread.Run(
            "constructing the board view",
            () => new WhiteboardView(Context));

        Assert(_boardView is not null, "new WhiteboardView(context) returned null.");
        Report("board view created — WebView and JS bridge live");
    }

    private static void ConstructsTheConfiguration()
    {
        // EnableInterrupterAPI off, as the façade sets it: with it on the SDK asks the app to
        // rewrite every resource URL through a callback, which is not something to leave on in a
        // check that never loads a document.
        var configuration = new WhiteSdkConfiguration(AppIdentifier)
        {
            EnableInterrupterAPI = false,
        };

        Assert(configuration is not null, "new WhiteSdkConfiguration(appIdentifier) returned null.");
        Report("configuration constructed from an unregistered App Identifier");
    }

    /// <summary>
    /// The SDK's constructor is what binds it to the view's JavaScript bridge — there is no
    /// separate setup call — so this is where a binding that lost the DSBridge base type would
    /// fail to compile, and where a JS bundle the view could not load would first misbehave. On
    /// the UI thread, because it touches the WebView it is given.
    /// </summary>
    private static void ConstructsTheSdk()
    {
        var configuration = new WhiteSdkConfiguration(AppIdentifier)
        {
            EnableInterrupterAPI = false,
        };

        _sdk = UiThread.Run(
            "constructing the SDK",
            () => new WhiteSdk(BoardView, Context, configuration));

        Assert(_sdk is not null, "new WhiteSdk(view, context, configuration) returned null.");
        Report("SDK bound to the board view");
    }

    /// <summary>
    /// The last thing that is answerable without a room: the parameters a join would take. Joining
    /// itself needs a room UUID and a room token minted by your own server against the whiteboard
    /// REST API — there is no client-side refusal to arrange, so this suite stops here rather than
    /// waiting on a request that could only fail for reasons about the network.
    /// </summary>
    private static void ConstructsRoomParameters()
    {
        var parameters = new RoomParams("net-agora-devicetests-room", AppIdentifier, "devicetests")
        {
            Writable = true,
        };

        Assert(parameters.Writable, "RoomParams.Writable did not round-trip.");
        Report("room parameters constructed; no join attempted");
    }

    /// <summary>
    /// The SDK has no release call of its own — its lifetime is the board view's, since what it
    /// holds is that WebView's JavaScript bridge — so destroying the view is the teardown, and it
    /// belongs on the UI thread like everything else that touches a WebView.
    /// </summary>
    private static void DestroysTheBoardView()
    {
        UiThread.Run("destroying the board view", () =>
        {
            BoardView.Destroy();
            BoardView.Dispose();
        });

        _boardView = null;
        _sdk = null;

        Report("board view destroyed");
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
