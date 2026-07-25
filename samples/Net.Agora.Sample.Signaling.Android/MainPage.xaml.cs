using System.Text;
using Agora.Rtm;

namespace Net.Agora.Sample.Signaling.Android;

/// <summary>
/// A tiny chat room over Agora Signaling, driven through the <b>raw</b> <c>Agora.Rtm</c> binding
/// rather than the cross-platform façade — the point of this sample is to show what
/// <c>Net.Agora.Signaling</c> hides.
///
/// Every operation here reports through a Java <c>ResultCallback</c> (login, subscribe, publish),
/// and events arrive on an <c>RtmEventListener</c> the SDK calls on its own thread. The façade in
/// <c>sbokatuk/Net.Agora</c> turns all of that into awaitable calls and ordinary .NET events; if
/// that is what you want, use <c>Net.Agora.Signaling</c> instead, which
/// <c>samples/Net.Agora.Sample.Signaling</c> there demonstrates.
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

    private void OnLoginClicked(object sender, EventArgs e)
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
            var config = new RtmConfig.Builder(appId, userId)
                .EventListener(new Listener(this))
                .Build();

            _client = RtmClient.Create(config);
            if (_client is null)
            {
                Append("RtmClient.Create returned null");
                LoginButton.IsEnabled = true;
                return;
            }

            _channel = channel;

            // Login is fire-and-forget: the result arrives on the callback, not from this call.
            // An App ID-only project logs in with the App ID as the token.
            _client.Login(appId, new Callback(
                onSuccess: () => Subscribe(channel),
                onFailure: reason =>
                {
                    Append($"login failed: {reason}");
                    Reset();
                }));
        }
        catch (Java.Lang.Exception exception)
        {
            Append($"login threw: {exception.Message}");
            Reset();
        }
    }

    private void Subscribe(string channel) =>
        _client?.Subscribe(channel, new SubscribeOptions(), new Callback(
            onSuccess: () => MainThread.BeginInvokeOnMainThread(() =>
            {
                Append($"logged in and subscribed to {channel}");
                SetLoggedIn(true);
            }),
            onFailure: reason => Append($"subscribe failed: {reason}")));

    private void OnLogoutClicked(object sender, EventArgs e)
    {
        if (_client is not null && _channel is not null)
        {
            _client.Unsubscribe(_channel, new Callback(
                onSuccess: () => Append($"unsubscribed from {_channel}"),
                onFailure: reason => Append($"unsubscribe failed: {reason}")));
        }

        _client?.Logout(null);
        // RtmClient is a process-wide singleton, released rather than disposed.
        RtmClient.Release();
        _client = null;
        _channel = null;
        Append("logged out");
        SetLoggedIn(false);
    }

    private void OnSendClicked(object sender, EventArgs e)
    {
        var message = MessageEntry.Text;
        if (_client is null || _channel is null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _client.Publish(_channel, message, new PublishOptions(), new Callback(
            onSuccess: () => MainThread.BeginInvokeOnMainThread(() =>
            {
                // A publisher does not receive its own message back — echo it locally.
                Append($"you: {message}");
                MessageEntry.Text = "";
            }),
            onFailure: reason => Append($"send failed: {reason}")));
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

    /// <summary>One RTM operation's completion — the Java ResultCallback the façade hides.</summary>
    private sealed class Callback(Action onSuccess, Action<string> onFailure)
        : Java.Lang.Object, IResultCallback
    {
        public void OnSuccess(Java.Lang.Object? responseInfo) => onSuccess();

        public void OnFailure(ErrorInfo? errorInfo) =>
            onFailure(errorInfo?.ErrorReason ?? "unknown error");
    }

    /// <summary>The event listener the SDK calls on its own thread.</summary>
    private sealed class Listener(MainPage owner) : Java.Lang.Object, IRtmEventListener
    {
        public void OnMessageEvent(MessageEvent? e)
        {
            if (e?.ChannelName is not { } channel)
            {
                return;
            }

            var text = e.Message?.Data is Java.Lang.String s ? s.ToString() : "[binary]";
            owner.Append($"{e.PublisherId}: {text}");
        }

        public void OnConnectionStateChanged(
            string? channelName,
            RtmConstants.RtmConnectionState? state,
            RtmConstants.RtmConnectionChangeReason? reason) =>
            owner.Append($"connection: {state}");

        public void OnTokenPrivilegeWillExpire(string? channelName) =>
            owner.Append("token expires soon — renew it");
    }
}
