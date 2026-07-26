using Agora.Rtc;
using Agora.Rtc.Video;
using Agora.Rtm;

namespace Net.Agora.Sample.Android;

/// <summary>
/// Creates <see cref="RtcEngine"/> directly against the raw binding and shows the local camera
/// preview, with the front/back flip the raw surface exposes. Deliberately minimal: no channel
/// is joined, so it runs with nothing but an App ID — the full join/publish/subscribe flow,
/// wrapped behind a cross-platform API, is Net.Agora/samples/Net.Agora.Sample in the façade
/// repository.
///
/// The Signaling button drives <see cref="RtmClient"/> from the same app: the two products
/// coexist (different Java packages, different native libraries), which this sample proves at
/// dex-merge time just by building.
/// </summary>
public partial class MainPage : ContentPage
{
    private RtcEngine? _engine;

    public MainPage()
    {
        InitializeComponent();

        // Static, so it works before any engine exists — and proves the .aar is wired up the
        // moment the page appears.
        StatusLabel.Text = $"native SDK {RtcEngine.SdkVersion}";
    }

    private async void OnStartPreviewClicked(object sender, EventArgs e)
    {
        var appId = AppIdEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appId))
        {
            Append("enter an App ID first");
            return;
        }

        // Dangerous (runtime) permissions: the manifest declaration alone does not grant them.
        // Microphone as well as camera — the engine opens the audio device even for a
        // preview-only session.
        var camera = await Permissions.RequestAsync<Permissions.Camera>();
        var microphone = await Permissions.RequestAsync<Permissions.Microphone>();
        if (camera != PermissionStatus.Granted || microphone != PermissionStatus.Granted)
        {
            Append("camera and microphone permission denied");
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
            return;
        }

        // The binding's C# event replaces the IRtcEngineEventHandler subclass this sample used
        // to carry. The SDK raises it on its own thread; Append hops to the main thread itself.
        engine.Error += (_, error) =>
            Append($"error {error.Code}: {RtcEngine.GetErrorDescription(error.Code)}");

        // The handler's platform view is the SurfaceView itself, which is exactly what
        // VideoCanvas wants — see AgoraVideoView.cs. The handler exists by now: this runs from a
        // click on the page it lives in.
        var surface = (global::Android.Views.SurfaceView)LocalView.Handler!.PlatformView!;
        engine.SetupLocalVideo(new VideoCanvas(surface, VideoCanvas.RenderModeHidden, 0));
        engine.EnableVideo();
        engine.StartPreview();

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

    private async void OnSignalingClicked(object sender, EventArgs e)
    {
        var appId = AppIdEntry.Text?.Trim();
        if (string.IsNullOrEmpty(appId))
        {
            Append("enter an App ID first");
            return;
        }

        // The raw RTM surface, from the same process the RTC engine runs in. Login is a live
        // signalling call; with an unregistered App ID it answers an error, which is exactly
        // what this demo shows — the round trip, credentials or not. LoginAsync (the binding's
        // own Task adapter over the ResultCallback overload) reports that answer by faulting
        // with RtmOperationException, so the exchange reads as one try/catch.
        RtmClient? rtm = null;
        try
        {
            var config = new RtmConfig.Builder(appId, "net-agora-sample").Build();
            rtm = RtmClient.Create(config);
            Append($"RTM created — logging in…");

            // An App ID-only project logs in with the App ID as the token.
            await rtm!.LoginAsync(appId);
            Append("RTM login succeeded");
            // Teardown; the result is deliberately ignored, as it always was here — the demo is
            // the login round trip.
            rtm.Logout(null);
        }
        catch (RtmOperationException exception)
        {
            Append($"RTM login answered: {exception.ErrorInfo?.ErrorReason ?? "unknown"}");
        }
        catch (Java.Lang.Exception exception)
        {
            Append($"RTM rejected the configuration: {exception.Message}");
        }
        finally
        {
            if (rtm is not null)
            {
                RtmClient.Release();
            }
        }
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
        _engine?.StopPreview();
        RtcEngine.Destroy();
        _engine = null;
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
