using System.Text;
using Agora.Rtm;

namespace Net.Agora.Sample.Signaling.Android;

/// <summary>
/// A tiny chat room over Agora Signaling, driven through the <b>raw</b> <c>Agora.Rtm</c> binding
/// rather than the cross-platform façade — the point of this sample is to show the binding's own
/// ergonomics.
///
/// Every operation (login, subscribe, publish, …) is awaited through the binding's Task adapters
/// (<c>LoginAsync</c> and friends, hand-written Additions over the SDK's Java
/// <c>ResultCallback</c> overloads) and fails by throwing <see cref="RtmOperationException"/>;
/// incoming traffic arrives through the C# events the binding generates on
/// <see cref="RtmClient"/> — no callback or listener classes to write. The SDK still raises
/// those events on its own thread, so UI updates hop through <see cref="Append"/>. The
/// cross-platform façade in <c>sbokatuk/Net.Agora</c> (package <c>Net.Agora.Signaling</c>) adds
/// the same shape across platforms; use it when the app is not Android-only.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _log = new();
    private RtmClient? _client;
    private string? _channel;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var appId = AppIdEntry.Text?.Trim();
        var userId = UserIdEntry.Text?.Trim();
        var channel = ChannelEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(channel))
        {
            Append("enter an App ID, a user id and a channel first");
            return;
        }

        LoginButton.IsEnabled = false;

        try
        {
            // No .EventListener(...) on the builder: the generated RtmClient exposes the same
            // callbacks as C# events, subscribed below once the client exists.
            var config = new RtmConfig.Builder(appId, userId).Build();

            _client = RtmClient.Create(config);
            if (_client is null)
            {
                Append("RtmClient.Create returned null");
                LoginButton.IsEnabled = true;
                return;
            }

            _channel = channel;

            _client.MessageEvent += OnMessage;
            _client.ConnectionStateChanged += (_, change) => Append($"connection: {change.State}");
            _client.TokenPrivilegeWillExpire += (_, _) => Append("token expires soon — renew it");

            // An App ID-only project logs in with the App ID as the token. Awaited straight
            // through: the Task completes when the SDK's ResultCallback fires, and a failure
            // arrives as RtmOperationException in the catch below.
            await _client.LoginAsync(appId);
            await _client.SubscribeAsync(channel);

            Append($"logged in and subscribed to {channel}");
            SetLoggedIn(true);
        }
        catch (RtmOperationException exception)
        {
            Append($"login failed: {exception.ErrorInfo?.ErrorReason ?? exception.Message}");
            Reset();
        }
        catch (Java.Lang.Exception exception)
        {
            Append($"login threw: {exception.Message}");
            Reset();
        }
    }

    private void OnMessage(object? sender, MessageEventEventArgs e)
    {
        if (e.Event?.ChannelName is not { } channel)
        {
            return;
        }

        var text = e.Event.Message?.Data is Java.Lang.String s ? s.ToString() : "[binary]";
        Append($"{e.Event.PublisherId}: {text}");
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        if (_client is not null && _channel is not null)
        {
            try
            {
                await _client.UnsubscribeAsync(_channel);
                Append($"unsubscribed from {_channel}");
            }
            catch (RtmOperationException exception)
            {
                Append($"unsubscribe failed: {exception.ErrorInfo?.ErrorReason ?? exception.Message}");
            }

            // The logout result is deliberately ignored, as it always was here — the session is
            // being torn down either way.
            _client.Logout(null);
        }

        // RtmClient is a process-wide singleton, released rather than disposed.
        RtmClient.Release();
        _client = null;
        _channel = null;
        Append("logged out");
        SetLoggedIn(false);
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        var message = MessageEntry.Text;
        if (_client is null || _channel is null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            await _client.PublishAsync(_channel, message);

            // A publisher does not receive its own message back — echo it locally.
            Append($"you: {message}");
            MessageEntry.Text = "";
        }
        catch (RtmOperationException exception)
        {
            Append($"send failed: {exception.ErrorInfo?.ErrorReason ?? exception.Message}");
        }
    }

    private void Reset()
    {
        RtmClient.Release();
        _client = null;
        _channel = null;
        MainThread.BeginInvokeOnMainThread(() => LoginButton.IsEnabled = true);
    }

    private void SetLoggedIn(bool loggedIn)
    {
        LoginButton.IsEnabled = !loggedIn;
        LogoutButton.IsEnabled = loggedIn;
        MessageEntry.IsEnabled = loggedIn;
        SendButton.IsEnabled = loggedIn;
    }

    private void Append(string message) => MainThread.BeginInvokeOnMainThread(() =>
    {
        _log.AppendLine($"{DateTime.Now:HH:mm:ss}  {message}");
        MessagesLabel.Text = _log.ToString();
        MessagesScroll.ScrollToAsync(0, MessagesLabel.Height, animated: false);
    });
}
