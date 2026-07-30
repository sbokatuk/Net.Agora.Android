using Agora.Fastboard;
using Agora.Fastboard.Model;
// Bare "Agora.Whiteboard.Domain" below would otherwise resolve as Net.Agora.Whiteboard.Domain:
// this file's own namespace starts with Net.Agora, and C#'s namespace lookup searches enclosing
// namespaces (here, the ancestor "Net.Agora") before the global one. The using directive sidesteps
// it because it is resolved from the global namespace regardless.
using Agora.Whiteboard.Domain;

namespace Net.Agora.Sample.Fastboard.Android;

/// <summary>
/// A whiteboard with Fastboard's own toolbar, driven through the <b>raw</b> <c>Agora.Fastboard</c>
/// binding rather than the cross-platform façade — the point of this sample is to show what
/// <c>Net.Agora.Fastboard</c> hides: the app owns the <see cref="FastboardView"/>, constructs
/// <c>Agora.Fastboard.Fastboard</c> against it directly, and the room's lifecycle — ready,
/// error, kicked, phase — arrives through <see cref="IFastRoomListener"/> rather than .NET events.
///
/// The three identifiers are the same three the plain Whiteboard sample uses: the App Identifier
/// from the Agora Console under Interactive Whiteboard, and the room UUID and token from your own
/// server's call to the whiteboard REST API.
/// </summary>
public partial class MainPage : ContentPage
{
    private FastRoom? _fastRoom;
    private bool _writable = true;

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

        if (Board.Board is not { } fastboardView)
        {
            Status("the board view has no native counterpart yet");
            return;
        }

        JoinButton.IsEnabled = false;

        // CnHz (mainland China) is Fastboard's own default region; a production app picks whichever
        // matches where its whiteboard room was created.
        var options = new FastRoomOptions(appIdentifier, roomUuid, roomToken, "android-user", FastRegion.CnHz!);
        // global:: because both "Agora.Fastboard.Fastboard" and the bare "Fastboard" this file's
        // "using Agora.Fastboard" would otherwise bring into scope are ambiguous here: this file's
        // own namespace starts with Net.Agora, and the binding assembly's generated resource
        // designer class declares a real Net.Agora.Fastboard.Android namespace chain, which the
        // compiler's enclosing-namespace lookup finds first.
        var fastboard = new global::Agora.Fastboard.Fastboard(fastboardView);

        _fastRoom = fastboard.CreateFastRoom(options);
        _fastRoom.AddListener(new RoomListener(this));

        // Asynchronous underneath: Join() itself returns nothing without a callback, and the
        // outcome otherwise arrives on IFastRoomListener below. The callback overload used here
        // additionally fires once the room is ready to draw on.
        _fastRoom.Join(new RoomReadyCallback(this));
        Status("joining…");
    }

    private void OnLeaveClicked(object sender, EventArgs e)
    {
        _fastRoom?.Disconnect();
        _fastRoom?.Destroy();
        _fastRoom = null;

        Status("left the room");
        SetJoined(false);
    }

    private void OnReadOnlyClicked(object sender, EventArgs e)
    {
        if (_fastRoom is null)
        {
            return;
        }

        // Fastboard hides its own drawing controls while read-only, so the toolbar follows this
        // without the app touching it.
        _writable = !_writable;
        _fastRoom.SetWritable(_writable, result: null);

        ReadOnlyButton.Text = _writable ? "Read-only" : "Writable";
        Status(_writable ? "you can draw" : "you are a viewer");
    }

    // Driving the board from code while the toolbar is on screen: both change the same state, and
    // the toolbar updates to match.
    private void OnRedPencilClicked(object sender, EventArgs e)
    {
        if (_fastRoom is null)
        {
            return;
        }

        _fastRoom.SetAppliance(FastAppliance.Pencil!);
        _fastRoom.SetStrokeColor(unchecked((int)0xFFE81123));
        Status("tool: red pencil");
    }

    private void SetJoined(bool joined)
    {
        JoinButton.IsEnabled = !joined;
        LeaveButton.IsEnabled = joined;
        ReadOnlyButton.IsEnabled = joined;
        RedPencilButton.IsEnabled = joined;
    }

    private void Status(string message) =>
        MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {message}");

    private sealed class RoomReadyCallback(MainPage owner) : Java.Lang.Object, IOnRoomReadyCallback
    {
        public void OnRoomReady(FastRoom fastRoom)
        {
            owner._writable = true;
            owner.Status("joined — the toolbar is live");
            MainThread.BeginInvokeOnMainThread(() => owner.SetJoined(true));
        }
    }

    /// <summary>Every member is required: a room's outcome has nowhere else to go.</summary>
    private sealed class RoomListener(MainPage owner) : Java.Lang.Object, IFastRoomListener
    {
        public void OnFastError(FastException error)
        {
            owner.Status($"failed: {error.Message}");
            MainThread.BeginInvokeOnMainThread(() => owner.JoinButton.IsEnabled = true);
        }

        public void OnRoomPhaseChanged(RoomPhase phase) =>
            owner.Status($"room: {phase}");

        public void OnRoomStateChanged(RoomState state)
        {
            // Not shown: this sample tracks phase and errors only.
        }

        public void OnRoomReadyChanged(FastRoom fastRoom)
        {
            // Handled by the Join(IOnRoomReadyCallback) callback instead.
        }

        public void OnRedoUndoChanged(FastRedoUndo count)
        {
            // Not shown: Fastboard's own toolbar already reflects this.
        }

        public void OnFastStyleChanged(FastStyle style)
        {
            // Not shown: this sample does not customise Fastboard's own styling.
        }

        public void OnOverlayChanged(int key)
        {
            // Not shown: this sample registers no custom overlays.
        }
    }
}
