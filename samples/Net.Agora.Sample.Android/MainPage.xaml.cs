using Agora.Rtc;
using Agora.Rtc.Video;

namespace Net.Agora.Sample.Android;

/// <summary>
/// Creates <see cref="RtcEngine"/> directly against the raw binding and shows the local camera
/// preview, with the front/back flip the raw surface exposes. Channel and token are optional —
/// with them left blank this runs as a preview-only demo on nothing but an App ID; entered, it
/// joins that channel too, rendering the first remote user's video into the second view next to
/// the local one (same one-remote-view-per-session layout as Net.Agora.Sample.iOS), which is what
/// actually exercises the engine end to end rather than just standing it up. The full
/// join/publish/subscribe flow, wrapped behind a cross-platform API, is still
/// Net.Agora/samples/Net.Agora.Sample in the façade repository — this stays a compile/runtime
/// check on the raw binding, not a replacement for that sample.
/// </summary>
public partial class MainPage : ContentPage
{
    private RtcEngine? _engine;
    private int? _remoteUid;

    public MainPage()
    {
        InitializeComponent();

        // Static, so it works before any engine exists — and proves the .aar is wired up the
        // moment the page appears.
        StatusLabel.Text = $"native SDK {RtcEngine.SdkVersion}";
    }

    private async void OnStartPreviewClicked(object sender, EventArgs e)
    {
        if (!StartButton.IsEnabled)
        {
            return;
        }

        var appId = AppIdEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appId))
        {
            Append("enter an App ID first");
            return;
        }

        // Disabled immediately rather than only at the end via SetPreviewing: the permission
        // requests below are async, and a second tap landing during that window called
        // RtcEngine.Create() a second time — the engine is a process-wide singleton (see
        // StopEngine's Destroy comment), so a second Create()/JoinChannel without an intervening
        // Destroy() corrupts its state instead of failing cleanly. Confirmed via
        // agoraapi.log: two overlapping taps produced two onJoinChannelSuccess entries with
        // different uids from what looked like one button press, and onUserJoined never fired
        // afterwards even with a real second participant in the same channel.
        StartButton.IsEnabled = false;

        // Dangerous (runtime) permissions: the manifest declaration alone does not grant them.
        // Microphone as well as camera — the engine opens the audio device even for a
        // preview-only session.
        var camera = await Permissions.RequestAsync<Permissions.Camera>();
        var microphone = await Permissions.RequestAsync<Permissions.Microphone>();
        if (camera != PermissionStatus.Granted || microphone != PermissionStatus.Granted)
        {
            Append("camera and microphone permission denied");
            StartButton.IsEnabled = true;
            return;
        }

        // RtcEngineConfig binds each of its Java fields (mAppId, mContext, ...) twice: once as
        // the plain field (MAppId, settable) and once through a same-named read-only getter. Only
        // the M-prefixed field form has a setter. The current Activity rather than the
        // application Context: the engine itself does not require one, but the camera preview
        // surface generally does. No MEventHandler: the binding's C# events below install their
        // own handler through AddHandler, and create() itself only requires the context.
        var config = new RtcEngineConfig
        {
            MContext = Platform.CurrentActivity ?? global::Android.App.Application.Context,
            MAppId = appId,
        };

        var engine = RtcEngine.Create(config);
        if (engine is null)
        {
            Append("RtcEngine.Create returned null");
            StartButton.IsEnabled = true;
            return;
        }

        // The binding's C# event replaces the IRtcEngineEventHandler subclass this sample used
        // to carry. The SDK raises it on its own thread; Append hops to the main thread itself.
        engine.Error += (_, error) =>
            Append($"error {error.Code}: {RtcEngine.GetErrorDescription(error.Code)}");

        // Channel and token are optional (see the class doc) — these events only ever fire when
        // OnStartPreviewClicked's JoinChannel call below actually runs.
        engine.JoinChannelSuccess += (_, ev) => Append($"joined channel {ev.Channel}, uid {ev.Uid}");

        // Raised on the SDK's own thread, so the view touch below needs the same hop Append
        // gives itself — RemoteView's platform view exists for the whole session (see the XAML),
        // so it is safe to reach for here without checking Handler first. Only one remote view in
        // this sample — the first remote user gets it, same as Net.Agora.Sample.iOS.
        engine.UserJoined += (_, ev) => MainThread.BeginInvokeOnMainThread(() =>
        {
            Append($"remote user joined: {ev.Uid}");

            if (_remoteUid is null && RemoteView.Handler?.PlatformView is global::Android.Views.SurfaceView remoteSurface)
            {
                _remoteUid = ev.Uid;
                _engine?.SetupRemoteVideo(new VideoCanvas(remoteSurface, VideoCanvas.RenderModeHidden, ev.Uid));
            }
        });

        engine.UserOffline += (_, ev) => MainThread.BeginInvokeOnMainThread(() =>
        {
            Append($"remote user left: {ev.Uid}");
            if (_remoteUid == ev.Uid)
            {
                _remoteUid = null;
            }
        });

        // The handler's platform view is the SurfaceView itself, which is exactly what
        // VideoCanvas wants — see AgoraVideoView.cs. The handler exists by now: this runs from a
        // click on the page it lives in.
        var surface = (global::Android.Views.SurfaceView)LocalView.Handler!.PlatformView!;
        engine.SetupLocalVideo(new VideoCanvas(surface, VideoCanvas.RenderModeHidden, 0));
        engine.EnableVideo();
        engine.StartPreview();

        var channel = ChannelEntry.Text?.Trim();
        var token = TokenEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(channel))
        {
            // This plain JoinChannel overload (no ChannelMediaOptions) defaults the client role to
            // Audience, not Broadcaster — confirmed via agoraapi.log's setUserRoleStatus(role:2, …)
            // on a join neither sample asked for that role on. Audience neither publishes nor is
            // notified of other Audience members joining, so two Audience-role apps in the same
            // channel each connect fine but never see or hear each other, and neither one's local
            // capture is ever actually sent — exactly the "each only sees myself" symptom this
            // fixes. See Net.Agora.Sample.iOS's OnJoinClicked for the same fix.
            // SetClientRole takes the raw Agora constant as a plain int, not a bound enum: the
            // only enum the binding carries for it (IRtcEngineEventHandler.ClientRole) is marked
            // obsolete on this Android platform. 1 is CLIENT_ROLE_BROADCASTER, Agora's own stable
            // public constant, unchanged across SDK versions.
            engine.SetClientRole(1);

            var joinResult = engine.JoinChannel(token, channel, optionalInfo: null, uid: 0);
            Append(joinResult == 0 ? $"joining {channel}…" : $"JoinChannel returned {joinResult}");
        }

        _engine = engine;
        SetPreviewing(true);
        Append("previewing — Flip switches the camera");
    }

    private void OnFlipClicked(object sender, EventArgs e)
    {
        // Raw surface: an Agora error code comes back, 0 for success — the façade hides this.
        var code = _engine?.SwitchCamera() ?? -1;
        Append(code == 0 ? "camera switched" : $"switchCamera returned {code}");
    }

    private void OnStopClicked(object sender, EventArgs e)
    {
        StopEngine();
        SetPreviewing(false);
        Append("stopped");
    }

    private void StopEngine()
    {
        // Releases the camera and the native renderers — the engine holds a GL context that
        // managed collection alone does not reclaim. Destroy is static because the engine is a
        // process-wide singleton.
        _engine?.LeaveChannel();
        _engine?.StopPreview();
        RtcEngine.Destroy();
        _engine = null;
        _remoteUid = null;
    }

    private void SetPreviewing(bool previewing)
    {
        StartButton.IsEnabled = !previewing;
        StopButton.IsEnabled = previewing;
        FlipButton.IsEnabled = previewing;
    }

    private void Append(string message) => MainThread.BeginInvokeOnMainThread(() =>
        ActivityLabel.Text = $"{DateTime.Now:HH:mm:ss}  {message}");

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            StopEngine();
        }
    }
}
