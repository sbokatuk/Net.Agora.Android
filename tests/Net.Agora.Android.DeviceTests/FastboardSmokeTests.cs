// The Fastboard flavor's suite — see SmokeTests.cs for why each flavor carries a whole class of
// the same name behind a define instead of branching at runtime: with only
// Net.Agora.Fastboard.Android referenced, nothing under Agora.Rtc or Agora.Rtm resolves, and
// MainActivity stays flavor-blind.
#if AGORA_FASTBOARD
using Agora.Fastboard;
using Agora.Fastboard.Model;
using Agora.Whiteboard;

using Android.Views;

using Wendu.Dsbridge.Special;

using NativeFastboard = Agora.Fastboard.Fastboard;

namespace Net.Agora.Android.DeviceTests;

/// <summary>
/// End-to-end checks for the packaged Fastboard binding — netless's ready-made UI over the
/// Interactive Whiteboard: a board with a working toolbar rather than a bare canvas. It is the one
/// package here that stacks three payloads in one app: Fastboard's own .aar (Java plus the
/// layouts and drawables its toolbar is made of), the whiteboard .aar it wraps, and DSBridge
/// underneath both — so most of what this suite has to say is about whether all three arrived and
/// still reach each other.
/// </summary>
/// <remarks>
/// Nothing here needs real credentials and nothing here joins a room, for the same reason as the
/// Whiteboard flavor: a join needs a room UUID and room token minted by your own server against
/// the whiteboard REST API, and there is no client-side refusal to arrange, so the checks stop at
/// the last point that is answerable offline. The identifiers below are well-formed and belong to
/// nobody.
/// <para>
/// "Needs no network" is meant literally and was checked that way: the suite passes 7/7 on an
/// emulator with wifi and mobile data switched off.
/// </para>
/// <para>
/// The checks are ordered: the view has to exist before the board can be built over it, and both
/// before either is torn down. A failure early on therefore cascades, which is the intent — the
/// first failure is the informative one.
/// </para>
/// </remarks>
public static class SmokeTests
{
    /// <summary>
    /// Shaped like a netless App Identifier (<c>&lt;access key&gt;/&lt;secret&gt;</c>) and
    /// registered to nobody. Nothing below sends it anywhere.
    /// </summary>
    private const string AppIdentifier = "netagora0123456789ab/devicetests0123456";

    private const string RoomUuid = "net-agora-devicetests-room";
    private const string UserId = "devicetests";

    /// <summary>
    /// An AppCompat theme for the context the board is inflated with. Fastboard's own views are
    /// framework widgets, but AppCompat is in its dependency graph and its resources are merged
    /// into the app, so inflating against a themed context is what a real host Activity would do —
    /// and it costs nothing here. Resolved by name at runtime rather than through the generated
    /// Resource class so that this file compiles in a flavor where AppCompat is not referenced at
    /// all; if it cannot be resolved the raw context is used and the check says so.
    /// </summary>
    private const string ThemeName = "Theme.AppCompat.Light.NoActionBar";

    public static Action<string> Reporter { get; set; } = _ => { };

    private static void Report(string message) => Reporter(message);

    private static Context Context => global::Android.App.Application.Context;

    /// <summary>The board view every check after <see cref="ConstructsTheBoardView"/> shares.</summary>
    private static FastboardView? _boardView;

    private static FastboardView BoardView =>
        _boardView ?? throw new InvalidOperationException("the board view has not been created yet.");

    private static NativeFastboard? _fastboard;

    private static NativeFastboard Fastboard =>
        _fastboard ?? throw new InvalidOperationException("the board has not been created yet.");

    public static SmokeTest[] All =>
    [
        new("the Java entry points resolve from all three packaged .aars", JavaEntryPointsResolve),
        new("the toolbar's resources merged into the app", TheToolbarResourcesMergedIntoTheApp),
        new("constructs the board view on the UI thread", ConstructsTheBoardView),
        new("the board view exposes the whiteboard view and the toolbar settings", ExposesTheWhiteboardView),
        new("constructs the board over the view and reports its version", ConstructsTheBoard),
        new("constructs room options without joining", ConstructsRoomOptions),
        new("destroys the board view", DestroysTheBoardView),
    ];

    /// <summary>
    /// Proves the .aars actually made it into the app — same reasoning as the RTC flavor's check
    /// (see SmokeTests.cs), over all three archives this app ends up holding.
    /// </summary>
    /// <remarks>
    /// The Java packages differ per layer, and each rename is only skin deep: io.agora.board.fast
    /// is Agora's own naming and becomes Agora.Fastboard in C#; com.herewhite.sdk is netless's
    /// original company name and comes from the whiteboard package this one depends on;
    /// wendu.dsbridge.special is the JS bridge that travels inside that package. All three have to
    /// be present for a board to exist, and only the first is this package's own — which is why
    /// the other two are checked here rather than trusted.
    /// </remarks>
    private static void JavaEntryPointsResolve()
    {
        string[] classes =
        [
            "io.agora.board.fast.Fastboard",
            "io.agora.board.fast.FastboardView",
            "io.agora.board.fast.FastRoom",
            "io.agora.board.fast.model.FastRoomOptions",
            "com.herewhite.sdk.WhiteboardView",
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
    /// This package's equivalent of the RTC flavor's native-library check. Fastboard ships no .so
    /// and no JavaScript of its own — what it ships besides Java is a toolbar: layouts, drawables
    /// and colours. Those are merged into the app by the Android resource pipeline rather than
    /// carried by the binding assembly, so they are the part of the payload a packaging change
    /// could drop without the binding noticing.
    /// </summary>
    /// <remarks>
    /// By name through the resource table rather than through the generated Resource class, for
    /// the same reason as the theme lookup: no compile-time dependency on which packages a given
    /// flavor happens to reference. Constructing the view below inflates the first of these
    /// anyway, but a missing resource surfaces there as an InflateException naming an integer.
    /// </remarks>
    private static void TheToolbarResourcesMergedIntoTheApp()
    {
        (string Type, string Name)[] resources =
        [
            ("layout", "layout_fastboard_view"),
            ("layout", "layout_toolbox_expand"),
            ("drawable", "fast_ic_tool_eraser"),
            ("color", "fast_day_night_bg"),
        ];

        var packageName = Context.PackageName!;
        var missing = resources
            .Where(resource => Context.Resources!.GetIdentifier(resource.Name, resource.Type, packageName) == 0)
            .Select(resource => $"{resource.Type}/{resource.Name}")
            .ToList();

        Assert(missing.Count == 0, $"these resources are not in the app: {string.Join(", ", missing)}");
        Report($"all {resources.Length} sampled toolbar resources resolved");
    }

    /// <summary>
    /// The board view is a FrameLayout that inflates Fastboard's own layout tree — including the
    /// whiteboard WebView underneath it — so constructing it on the UI thread is both what Android
    /// requires and the single check with the most of this package behind it.
    /// </summary>
    private static void ConstructsTheBoardView()
    {
        var themeId = Context.Resources!.GetIdentifier(ThemeName, "style", Context.PackageName!);
        var context = themeId == 0 ? Context : new ContextThemeWrapper(Context, themeId);

        _boardView = UiThread.Run(
            "constructing the board view",
            () => new FastboardView(context));

        Assert(_boardView is not null, "new FastboardView(context) returned null.");
        Report(themeId == 0
            ? $"board view created against an unthemed context ({ThemeName} did not resolve)"
            : $"board view created against {ThemeName}");
    }

    /// <summary>
    /// The Fastboard-side view of the trap the Whiteboard flavor checks from the other end. Its
    /// public API takes and returns com.herewhite.sdk types, so if DSBridge were carried but not
    /// bound — leaving the generator to drop WhiteboardView and every method mentioning one — this
    /// property would not exist to call. That it answers a live view proves the whole stack is
    /// bound, not merely packaged.
    /// </summary>
    private static void ExposesTheWhiteboardView()
    {
        var whiteboardView = BoardView.WhiteboardView;

        Assert(whiteboardView is not null, "FastboardView.WhiteboardView was null.");
        Assert(
            whiteboardView is DWebView,
            $"FastboardView.WhiteboardView is a {whiteboardView!.GetType().FullName}, not a DSBridge DWebView.");

        // The toolbar half of the same question: the UI settings object is what a consumer drives
        // the ready-made toolbar through, and it is created as part of inflating the view.
        Assert(BoardView.UiSettings is not null, "FastboardView.UiSettings was null.");

        Report("the board view exposes a bound WhiteboardView and its UI settings");
    }

    private static void ConstructsTheBoard()
    {
        // On the UI thread: the board takes the view and reaches into it, so it belongs on the
        // thread that owns it, exactly as a host Activity would do it.
        _fastboard = UiThread.Run(
            "constructing the board",
            () => new NativeFastboard(BoardView));

        Assert(_fastboard is not null, "new Fastboard(view) returned null.");

        var version = Fastboard.Version;
        Assert(!string.IsNullOrWhiteSpace(version), "Fastboard.Version was null or empty.");

        Report($"board created — SDK version {version}");
    }

    /// <summary>
    /// The last thing that is answerable without a room: the options a join would take, including
    /// the whiteboard configuration nested inside them — which is another place Fastboard's API
    /// hands out a com.herewhite.sdk type, and so another place an unbound layer would show.
    /// CreateFastRoom and Join are deliberately not called: they are a live exchange with netless's
    /// servers, refused for reasons about the network rather than about this package.
    /// </summary>
    private static void ConstructsRoomOptions()
    {
        var options = new FastRoomOptions(
            AppIdentifier,
            RoomUuid,
            AppIdentifier,
            UserId,
            FastRegion.CnHz!,
            true)
        {
            SdkConfiguration = new WhiteSdkConfiguration(AppIdentifier)
            {
                EnableInterrupterAPI = false,
            },
        };

        Assert(options.AppId == AppIdentifier, $"FastRoomOptions.AppId answered '{options.AppId}'.");
        Assert(options.Uuid == RoomUuid, $"FastRoomOptions.Uuid answered '{options.Uuid}'.");
        Assert(options.IsWritable, "FastRoomOptions.IsWritable did not round-trip.");
        Assert(options.SdkConfiguration is not null, "FastRoomOptions.SdkConfiguration did not round-trip.");

        Report($"room options constructed for region {options.FastRegion}; no join attempted");
    }

    /// <summary>
    /// Neither the board nor the SDK under it has a release call — their lifetime is the view's,
    /// since what they hold is that WebView's JavaScript bridge — so destroying the view is the
    /// teardown, on the UI thread like everything else that touches it.
    /// </summary>
    private static void DestroysTheBoardView()
    {
        UiThread.Run("destroying the board view", () =>
        {
            BoardView.WhiteboardView?.Destroy();
            BoardView.RemoveAllViews();
            BoardView.Dispose();
        });

        _boardView = null;
        _fastboard = null;

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
