using Agora.Rtc;
using Agora.Rtc.Video;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace Net.Agora.Sample.Android;

/// <summary>
/// Joins an Agora RTC channel, publishing this device's camera/microphone and rendering the first
/// remote user's video — the same flow as Net.Agora/samples/Net.Agora.Sample, but built directly
/// against <see cref="RtcEngine"/> rather than the cross-platform façade, since that façade lives
/// in a separate repository this one does not depend on.
/// </summary>
[Activity(Label = "Net.Agora Sample", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
public class MainActivity : Activity
{
    private const int PermissionRequestCode = 100;

    private readonly EditText _appIdInput;
    private readonly EditText _channelIdInput;
    private readonly EditText _tokenInput;
    private readonly SurfaceView _localView;
    private readonly SurfaceView _remoteView;
    private readonly Button _joinButton;
    private readonly Button _leaveButton;
    private readonly TextView _statusView;

    private RtcEngine? _engine;
    private uint? _remoteUid;

    public MainActivity()
    {
        _appIdInput = new EditText(this) { Hint = "Agora App ID" };
        _channelIdInput = new EditText(this) { Hint = "channel id" };
        _tokenInput = new EditText(this) { Hint = "token (optional — App ID-only auth is testing-only)" };
        _localView = new SurfaceView(this);
        _remoteView = new SurfaceView(this);
        _joinButton = new Button(this) { Text = "Join" };
        _leaveButton = new Button(this) { Text = "Leave", Enabled = false };
        _statusView = new TextView(this) { TextSize = 12 };
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _joinButton.Click += OnJoinClicked;
        _leaveButton.Click += OnLeaveClicked;

        SetContentView(BuildLayout());
    }

    // Both SurfaceViews exist for the whole session: creating one on demand would mean creating a
    // native renderer mid-layout, which the SDK does not expect — same reasoning as
    // Net.Agora.Video.Maui's AgoraVideoView in the façade repo.
    private View BuildLayout()
    {
        var videoRow = new LinearLayout(this) { Orientation = global::Android.Widget.Orientation.Horizontal };
        videoRow.AddView(_localView, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1f));
        videoRow.AddView(_remoteView, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1f));

        var buttonRow = new LinearLayout(this) { Orientation = global::Android.Widget.Orientation.Horizontal };
        buttonRow.AddView(_joinButton);
        buttonRow.AddView(_leaveButton);

        var statusScroll = new ScrollView(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 300),
        };
        statusScroll.AddView(_statusView);

        var root = new LinearLayout(this) { Orientation = global::Android.Widget.Orientation.Vertical };
        root.SetPadding(32, 32, 32, 32);
        root.AddView(_appIdInput);
        root.AddView(_channelIdInput);
        root.AddView(_tokenInput);
        root.AddView(videoRow, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
        root.AddView(buttonRow);
        root.AddView(statusScroll);
        return root;
    }

    private void OnJoinClicked(object? sender, EventArgs e)
    {
        var appId = _appIdInput.Text?.Trim();
        var channelId = _channelIdInput.Text?.Trim();

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(channelId))
        {
            Append("enter an App ID and a channel id first");
            return;
        }

        if (!HasCapturePermissions())
        {
            RequestPermissions([global::Android.Manifest.Permission.Camera, global::Android.Manifest.Permission.RecordAudio], PermissionRequestCode);
            Append("camera and microphone permission requested — tap Join again once granted");
            return;
        }

        Join(appId, channelId);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode != PermissionRequestCode)
        {
            return;
        }

        Append(grantResults.All(result => result == Permission.Granted)
            ? "permissions granted — tap Join again"
            : "camera and microphone permission denied");
    }

    private bool HasCapturePermissions() =>
        CheckSelfPermission(global::Android.Manifest.Permission.Camera) == Permission.Granted &&
        CheckSelfPermission(global::Android.Manifest.Permission.RecordAudio) == Permission.Granted;

    private void Join(string appId, string channelId)
    {
        var token = string.IsNullOrEmpty(_tokenInput.Text) ? null : _tokenInput.Text!.Trim();

        // RtcEngineConfig binds each of its Java fields (mAppId, mContext, ...) twice: once as the
        // plain field (settable) and once through a same-named read-only getter — see the façade
        // repo's Platforms/Android/AgoraVideoClient.cs for the same note.
        var config = new RtcEngineConfig
        {
            MContext = this,
            MAppId = appId,
            MEventHandler = new EngineHandler(this),
        };

        var engine = RtcEngine.Create(config);
        if (engine is null)
        {
            Append("RtcEngine.Create returned null");
            return;
        }

        engine.SetupLocalVideo(new VideoCanvas(_localView, VideoCanvas.RenderModeHidden, 0));
        engine.EnableVideo();
        engine.JoinChannel(token, channelId, null, 0);

        _engine = engine;
        SetJoined(true);
    }

    private void OnLeaveClicked(object? sender, EventArgs e)
    {
        _engine?.LeaveChannel();
        RtcEngine.Destroy();
        _engine = null;
        _remoteUid = null;
        SetJoined(false);
        Append("left");
    }

    private void SetJoined(bool joined)
    {
        _joinButton.Enabled = !joined;
        _leaveButton.Enabled = joined;
    }

    private void Append(string message) =>
        _statusView.Text += $"{DateTime.Now:HH:mm:ss}  {message}\n";

    protected override void OnDestroy()
    {
        base.OnDestroy();

        // Releases the camera and the native renderers — the engine holds a GL context that
        // managed collection alone does not reclaim.
        _engine?.LeaveChannel();
        RtcEngine.Destroy();
    }

    /// <summary>
    /// Translates <c>IRtcEngineEventHandler</c>'s callbacks — a Java abstract class overridden
    /// per-instance, not a .NET event — into UI updates. The SDK raises these on its own thread,
    /// so anything touching the UI hops back with <see cref="Activity.RunOnUiThread(Action)"/>.
    /// </summary>
    private sealed class EngineHandler(MainActivity owner) : IRtcEngineEventHandler
    {
        public override void OnJoinChannelSuccess(string channel, int uid, int elapsed) =>
            owner.RunOnUiThread(() => owner.Append($"joined {channel} as {uid}"));

        public override void OnLeaveChannel(IRtcEngineEventHandler.RtcStats stats) =>
            owner.RunOnUiThread(() => owner.Append("left channel"));

        public override void OnUserJoined(int uid, int elapsed) => owner.RunOnUiThread(() =>
        {
            owner.Append($"remote user joined: {uid}");

            // Only one remote view in this sample — the first remote user gets it.
            if (owner._remoteUid is null)
            {
                owner._remoteUid = (uint)uid;
                owner._engine?.SetupRemoteVideo(new VideoCanvas(owner._remoteView, VideoCanvas.RenderModeHidden, uid));
            }
        });

        public override void OnUserOffline(int uid, int reason) => owner.RunOnUiThread(() =>
        {
            owner.Append($"remote user left: {uid}");
            if (owner._remoteUid == (uint)uid)
            {
                owner._remoteUid = null;
            }
        });

        public override void OnError(int err) => owner.RunOnUiThread(() =>
            owner.Append($"error {err}: {RtcEngine.GetErrorDescription(err)}"));
    }
}
