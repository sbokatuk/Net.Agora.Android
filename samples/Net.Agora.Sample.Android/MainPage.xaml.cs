using Agora.Rtc;
using Agora.Rtc.Video;

namespace Net.Agora.Sample.Android;

/// <summary>
/// Creates <see cref="RtcEngine"/> directly against the raw binding and shows the local camera
/// preview. Deliberately minimal: no channel is joined, so it runs with nothing but an App ID —
/// the full join/publish/subscribe flow, wrapped behind a cross-platform API, is
/// Net.Agora/samples/Net.Agora.Sample in the façade repository.
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
        // surface generally does.
        var config = new RtcEngineConfig
        {
            MContext = Platform.CurrentActivity ?? global::Android.App.Application.Context,
            MAppId = appId,
            MEventHandler = new EngineHandler(this),
        };

        var engine = RtcEngine.Create(config);
        if (engine is null)
        {
            Append("RtcEngine.Create returned null");
            return;
        }

        // The handler's platform view is the SurfaceView itself, which is exactly what
        // VideoCanvas wants — see AgoraVideoView.cs. The handler exists by now: this runs from a
        // click on the page it lives in.
        var surface = (global::Android.Views.SurfaceView)LocalView.Handler!.PlatformView!;
        engine.SetupLocalVideo(new VideoCanvas(surface, VideoCanvas.RenderModeHidden, 0));
        engine.EnableVideo();
        engine.StartPreview();

        _engine = engine;
        SetPreviewing(true);
        Append("previewing");
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

    /// <summary>
    /// Translates <c>IRtcEngineEventHandler</c>'s callbacks — a Java abstract class overridden
    /// per-instance, not a .NET event — into UI updates. The SDK raises these on its own thread,
    /// so anything touching the UI hops back through <see cref="Append"/>.
    /// </summary>
    private sealed class EngineHandler(MainPage owner) : IRtcEngineEventHandler
    {
        public override void OnError(int err) =>
            owner.Append($"error {err}: {RtcEngine.GetErrorDescription(err)}");
    }
}
