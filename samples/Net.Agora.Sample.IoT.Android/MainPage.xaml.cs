using System.Text;
using Agora.IoT;

namespace Net.Agora.Sample.IoT.Android;

/// <summary>
/// The smallest thing that proves <c>Net.Agora.IoT.Android</c> is a working binding: it
/// initialises the SDK against an App ID and an IoT project id, reports the state machine, and
/// releases it again.
///
/// Deliberately no further than that. Everything past initialisation — pairing a device, placing a
/// call to it, reading its alarms — needs a provisioned Agora IoT project with real hardware
/// behind it, and the device-messaging half routes through AWS IoT Core, whose Android SDK has no
/// .NET binding and is not referenced here. See the package's own description.
///
/// There is no iOS half of this sample, and no cross-platform client, because Agora ships no iOS
/// IoT SDK. This app also cannot reference any other Net.Agora package: the IoT .aar bundles its
/// own copies of the RTC and Signaling SDKs, so a second one fails the dex merge.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _log = new();
    private IAgoraIotAppSdk? _sdk;

    public MainPage()
    {
        InitializeComponent();
    }

    private void OnInitClicked(object sender, EventArgs e)
    {
        var appId = AppIdEntry.Text?.Trim();
        var projectId = ProjectIdEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(projectId))
        {
            Append("enter an App ID and an IoT project id first");
            return;
        }

        var sdk = AIotAppSdkFactory.Instance;
        if (sdk is null)
        {
            Append("AIotAppSdkFactory.Instance returned null");
            return;
        }

        // The SDK takes a Context, like the RTC engines do — hence a sample that is Android-only
        // all the way down rather than a MAUI page with an #if.
        var parameters = new IAgoraIotAppSdk.InitParam
        {
            MContext = Platform.CurrentActivity?.ApplicationContext,
            MRtcAppId = appId,
            MProjectID = projectId,
        };

        var result = sdk.Initialize(parameters);
        Append($"initialize returned {result}");

        if (result != 0)
        {
            return;
        }

        _sdk = sdk;
        InitButton.IsEnabled = false;
        StateButton.IsEnabled = true;
        ReleaseButton.IsEnabled = true;
    }

    private void OnStateClicked(object sender, EventArgs e)
    {
        if (_sdk is null)
        {
            return;
        }

        // The SDK's own SDK_STATE_* constants, spelled out rather than printed raw — the numbers
        // on their own say nothing.
        var state = _sdk.StateMachine;
        Append($"state machine: {state} ({Describe(state)})");
    }

    private void OnReleaseClicked(object sender, EventArgs e)
    {
        _sdk?.Release();
        _sdk = null;

        Append("released");
        InitButton.IsEnabled = true;
        StateButton.IsEnabled = false;
        ReleaseButton.IsEnabled = false;
    }

    private static string Describe(int state) => state switch
    {
        IAgoraIotAppSdk.SdkStateInvalid => "invalid",
        IAgoraIotAppSdk.SdkStateReady => "ready — initialised, not logged in",
        IAgoraIotAppSdk.SdkStateLogining => "logging in",
        IAgoraIotAppSdk.SdkStateLogouting => "logging out",
        IAgoraIotAppSdk.SdkStateRunning => "running",
        _ => "unknown",
    };

    private void Append(string message) => MainThread.BeginInvokeOnMainThread(() =>
    {
        _log.AppendLine($"{DateTime.Now:HH:mm:ss}  {message}");
        StatusLabel.Text = _log.ToString();
    });

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            _sdk?.Release();
            _sdk = null;
        }
    }
}
