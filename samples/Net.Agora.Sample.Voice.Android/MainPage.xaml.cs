using Agora.Rtc;

namespace Net.Agora.Sample.Voice.Android;

/// <summary>
/// Creates <see cref="RtcEngine"/> directly against the raw Voice binding and drives the audio
/// surface — capture, mute, speakerphone routing, who-is-speaking volume reports. Channel and token
/// are optional, as in Net.Agora.Sample.Android: left blank this runs on nothing but an App ID;
/// entered, it joins that channel too, same flow as Net.Agora.Sample.Voice.iOS. The same flow
/// wrapped behind a cross-platform API is Net.Agora/samples/Net.Agora.Sample.Voice in the façade
/// repository.
///
/// The voice .aar carries the same Java API layer as the full one (video entry points included);
/// what it lacks is the native video pipeline — which is the size win a voice-only app picks
/// this package for.
/// </summary>
public partial class MainPage : ContentPage
{
    private RtcEngine? _engine;

    public MainPage()
    {
        InitializeComponent();

        SamplePrefill.Fill(AppIdEntry, "AppId");
        SamplePrefill.Fill(ChannelEntry, "Channel", secret: false);
        SamplePrefill.Fill(TokenEntry, "Token");

        // Static, so it works before any engine exists — and proves the .aar is wired up the
        // moment the page appears.
        StatusLabel.Text = $"native SDK {RtcEngine.SdkVersion}";
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        var appId = AppIdEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appId))
        {
            Append("enter an App ID first");
            return;
        }

        // A dangerous (runtime) permission: the manifest declaration alone does not grant it.
        if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted)
        {
            Append("microphone permission denied");
            return;
        }

        // RtcEngineConfig binds each of its Java fields (mAppId, mContext, ...) twice: once as
        // the plain field (MAppId, settable) and once through a same-named read-only getter.
        // Only the M-prefixed field form has a setter. Any Context serves — no camera surface
        // here, unlike the Video sample. No MEventHandler: the binding's C# events below install
        // their own handler through AddHandler, and create() itself only requires the context.
        var config = new RtcEngineConfig
        {
            MContext = global::Android.App.Application.Context,
            MAppId = appId,
        };

        var engine = RtcEngine.Create(config);
        if (engine is null)
        {
            Append("RtcEngine.Create returned null");
            return;
        }

        // The binding's C# events replace the IRtcEngineEventHandler subclass this sample used
        // to carry. The SDK raises them on its own thread, so the volume label hops to the main
        // thread explicitly (Append does its own hop).
        engine.Error += (_, error) =>
            Append($"error {error.Code}: {RtcEngine.GetErrorDescription(error.Code)}");
        engine.JoinChannelSuccess += (_, ev) => Append($"joined channel {ev.Channel}, uid {ev.Uid}");
        engine.UserJoined += (_, ev) => Append($"remote user joined: {ev.Uid}");
        engine.UserOffline += (_, ev) => Append($"remote user left: {ev.Uid}");
        engine.AudioVolumeIndication += (_, volume) => MainThread.BeginInvokeOnMainThread(() =>
        {
            // Uid 0 is this device. The SDK reports the loudest speakers, silent ones included.
            var speaking = (volume.Speakers ?? [])
                .Where(speaker => speaker.Volume > 0)
                .Select(speaker => speaker.Uid == 0 ? $"you ({speaker.Volume})" : $"{speaker.Uid} ({speaker.Volume})")
                .ToList();
            VolumeLabel.Text = speaking.Count > 0
                ? $"speaking: {string.Join(", ", speaking)}"
                : "nobody is speaking";
        });

        // Raw surface: every call answers an Agora error code, 0 for success — the façade hides
        // this. EnableAudio spins the capture pipeline up; the volume indication cadence drives
        // the who-is-speaking label (uid 0 is this device).
        engine.EnableAudio();
        engine.EnableAudioVolumeIndication(200, 3, false);

        var channel = ChannelEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(channel))
        {
            // 1 is CLIENT_ROLE_BROADCASTER. Without it this JoinChannel overload joins as Audience,
            // which publishes nothing and never hears of other Audience members. See
            // Net.Agora.Sample.Android's OnStartPreviewClicked for the long version.
            engine.SetClientRole(1);

            var joinResult = engine.JoinChannel(TokenEntry.Text?.Trim(), channel, optionalInfo: null, uid: 0);
            Append(joinResult == 0 ? $"joining {channel}…" : $"JoinChannel returned {joinResult}");
        }

        _engine = engine;
        SetRunning(true);
        Append("capturing — speak and watch the volume label");
    }

    private void OnStopClicked(object sender, EventArgs e)
    {
        StopEngine();
        SetRunning(false);
        Append("stopped");
    }

    private void OnMuteToggled(object sender, ToggledEventArgs e)
    {
        var code = _engine?.MuteLocalAudioStream(e.Value) ?? -1;
        Append(code == 0
            ? (e.Value ? "microphone muted" : "microphone unmuted")
            : $"muteLocalAudioStream returned {code}");
    }

    private void OnSpeakerToggled(object sender, ToggledEventArgs e)
    {
        // The Java method's casing ("Routeto") is Agora's own wart, faithfully preserved by the
        // binding. Outside a channel this sets the default route; the façade's SetSpeakerphone
        // switches to the live override once joined.
        var code = _engine?.SetDefaultAudioRoutetoSpeakerphone(e.Value) ?? -1;
        Append(code == 0
            ? (e.Value ? "default route: speaker" : "default route: earpiece")
            : $"setDefaultAudioRoutetoSpeakerphone returned {code}");
    }

    private void StopEngine()
    {
        // Destroy is static because the engine is a process-wide singleton; it blocks until the
        // native side has torn down.
        _engine?.LeaveChannel();
        RtcEngine.Destroy();
        _engine = null;
    }

    private void SetRunning(bool running)
    {
        StartButton.IsEnabled = !running;
        StopButton.IsEnabled = running;
        MuteSwitch.IsEnabled = running;
        SpeakerSwitch.IsEnabled = running;
        if (!running)
        {
            MuteSwitch.IsToggled = false;
            SpeakerSwitch.IsToggled = false;
            VolumeLabel.Text = "volume indication off";
        }
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
