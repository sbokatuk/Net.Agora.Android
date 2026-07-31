using Agora.Whiteboard;
using Agora.Whiteboard.Domain;

namespace Net.Agora.Sample.Whiteboard.Android;

/// <summary>
/// A shared drawing surface over the Agora Interactive Whiteboard, driven through the <b>raw</b>
/// <c>Agora.Whiteboard</c> binding rather than the cross-platform façade — the point of this
/// sample is to show what <c>Net.Agora.Whiteboard</c> hides: the app owns the
/// <c>Agora.Whiteboard.WhiteboardView</c> and constructs <see cref="WhiteSdk"/> against it
/// directly, and every call is netless's own <see cref="IPromise"/> shape (a <c>Then</c>/
/// <c>CatchEx</c> pair) rather than an awaitable Task.
///
/// The three identifiers come from two different places. The App Identifier is in the Agora
/// Console under Interactive Whiteboard; the room UUID and its token come from your own server's
/// call to the whiteboard REST API, because minting them needs a secret an app must not carry.
/// </summary>
public partial class MainPage : ContentPage
{
    private Room? _room;
    private RoomCallbacks? _callbacks;

    public MainPage()
    {
        InitializeComponent();
    }

    private void OnJoinClicked(object sender, EventArgs e)
    {
        var appIdentifier = AppIdentifierEntry.Text?.Trim();
        var roomUuid = RoomUuidEntry.Text?.Trim();
        var roomToken = RoomTokenEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appIdentifier) || string.IsNullOrEmpty(roomUuid) || string.IsNullOrEmpty(roomToken))
        {
            Status("enter an App Identifier, a room UUID and a room token first");
            return;
        }

        if (Board.Board is not { } boardView)
        {
            Status("the board view has no native counterpart yet");
            return;
        }

        JoinButton.IsEnabled = false;

        var config = new WhiteSdkConfiguration(appIdentifier);
        var sdk = new WhiteSdk(boardView, global::Android.App.Application.Context, config);
        var roomParams = new RoomParams(roomUuid, roomToken, "android-user");
        _callbacks = new RoomCallbacks(this);

        sdk.JoinRoom(roomParams, _callbacks, new JoinPromise(this));
        Status("joining…");
    }

    private void OnLeaveClicked(object sender, EventArgs e)
    {
        _room?.Disconnect();
        _room = null;
        _callbacks = null;

        Status("left the room");
        SetJoined(false);
    }

    private void OnPencilClicked(object sender, EventArgs e) => Tool("pencil", [232, 17, 35], "pencil");

    private void OnEraserClicked(object sender, EventArgs e) => Tool("eraser", strokeColor: null, "eraser");

    private void OnUndoClicked(object sender, EventArgs e) => _room?.Undo();

    private void OnRedoClicked(object sender, EventArgs e) => _room?.Redo();

    private void OnClearClicked(object sender, EventArgs e)
    {
        // retainPpt keeps a converted document's own content on the scene and clears only what was
        // drawn over it — there is no document here, so the flag makes no difference.
        _room?.CleanScene(retainPpt: true);
        Status("cleared the page for everyone");
    }

    private void Tool(string appliance, int[]? strokeColor, string name)
    {
        if (_room is null)
        {
            return;
        }

        var state = new MemberState { CurrentApplianceName = appliance, StrokeWidth = 4 };
        if (strokeColor is not null)
        {
            state.SetStrokeColor(strokeColor);
        }

        _room.MemberState = state;
        Status($"tool: {name}");
    }

    private void SetJoined(bool joined)
    {
        JoinButton.IsEnabled = !joined;
        LeaveButton.IsEnabled = joined;
        PencilButton.IsEnabled = joined;
        EraserButton.IsEnabled = joined;
        ClearButton.IsEnabled = joined;

        if (!joined)
        {
            UndoButton.IsEnabled = false;
            RedoButton.IsEnabled = false;
        }
    }

    private void Status(string message) =>
        MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {message}");

    /// <summary>The completion side of joinRoom: netless's promise shape, not a Task.</summary>
    private sealed class JoinPromise(MainPage owner) : Java.Lang.Object, IPromise
    {
        public void Then(Java.Lang.Object p0)
        {
            var room = p0 as Room;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                owner._room = room;

                // Known Android-only bug, fixed the same way in Net.Agora's façade
                // (Net.Agora.Whiteboard/Platforms/Android/AgoraWhiteboardClient.cs): the SDK's own
                // page never gives its #root / #whiteboard-container a height — they sit under a
                // plain, unstyled div — so the canvas inside inherits 0. A 0-height canvas neither
                // paints nor maps touch coordinates, so drawing silently does nothing and no undo
                // entry is ever recorded, with no error anywhere in logcat. window.innerHeight
                // itself is unaffected, so pull the real height from there rather than passing one
                // in from the native side.
                if (owner.Board.Board is { } boardView)
                {
                    boardView.CallFocusView();
                    boardView.EvaluateJavascript(FixContainerHeightScript, null);
                }

                owner.Status("joined — draw on the board");
                owner.SetJoined(true);
            });
        }

        public void CatchEx(SDKError error) => MainThread.BeginInvokeOnMainThread(() =>
        {
            owner.Status($"failed: {error.Message}");
            owner.JoinButton.IsEnabled = true;
        });
    }

    // See the comment on JoinPromise.Then for why this is necessary.
    private const string FixContainerHeightScript = """
        (function () {
            var h = window.innerHeight + 'px';
            ['root', 'whiteboard-container'].forEach(function (id) {
                var el = document.getElementById(id);
                if (el) { el.style.height = h; }
            });
            document.documentElement.style.height = h;
            document.body.style.height = h;
        })();
        """;

    /// <summary>
    /// Implements <see cref="IRoomListener"/> directly rather than subclassing the SDK's own
    /// <c>AbstractRoomCallbacks</c> convenience base — that base is obsoleted on this platform, so
    /// every member here is required, unlike Fastboard's equivalent listener on the Android side.
    /// </summary>
    private sealed class RoomCallbacks(MainPage owner) : Java.Lang.Object, IRoomListener
    {
        public void OnPhaseChanged(RoomPhase phase) => owner.Status($"room: {phase}");

        public void OnDisconnectWithError(Java.Lang.Exception error)
        {
            owner.Status($"disconnected: {error.Message}");
            MainThread.BeginInvokeOnMainThread(() => owner.SetJoined(false));
        }

        public void OnKickedWithReason(string reason)
        {
            owner.Status($"removed from the room: {reason}");
            MainThread.BeginInvokeOnMainThread(() => owner.SetJoined(false));
        }

        public void OnCanUndoStepsUpdate(long canUndoSteps) =>
            MainThread.BeginInvokeOnMainThread(() => owner.UndoButton.IsEnabled = canUndoSteps > 0);

        public void OnCanRedoStepsUpdate(long canRedoSteps) =>
            MainThread.BeginInvokeOnMainThread(() => owner.RedoButton.IsEnabled = canRedoSteps > 0);

        public void OnCatchErrorWhenAppendFrame(long userId, Java.Lang.Exception error)
        {
            // Not shown: this sample does not use the append-frame API.
        }

        public void OnRoomStateChanged(RoomState modifyState)
        {
            // Not shown: this sample tracks phase and errors only.
        }
    }
}
