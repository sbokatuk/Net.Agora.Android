using System.Text;
using Agora.Chat;
using IO.Agora;

namespace Net.Agora.Sample.Chat.Android;

/// <summary>
/// A tiny two-user chat over Agora Chat, driven through the <b>raw</b> <c>Agora.Chat</c> binding
/// rather than the cross-platform façade — the point of this sample is to show the binding's own
/// ergonomics.
///
/// Unlike the Signaling and Voice samples in this repository, nothing here is hand-wrapped into
/// Task-returning Additions: <see cref="ChatClient"/>'s request/response calls (login, logout,
/// send) still take the SDK's own <see cref="ICallBack"/> — three plain methods, no [Async]
/// counterpart — because that is exactly what the generated binding exposes. What the generator
/// does give for free is an ordinary .NET event for the one thing the SDK calls back on more than
/// once, which is why incoming messages arrive as <c>ChatManager.MessageReceived</c> rather than
/// through a listener class. The cross-platform façade in <c>sbokatuk/Net.Agora</c> (package
/// <c>Net.Agora.Chat</c>) layers Task-returning calls over the callback surface too.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _log = new();
    private string? _peer;

    public MainPage()
    {
        InitializeComponent();
    }

    private void OnLoginClicked(object sender, EventArgs e)
    {
        var appKey = AppKeyEntry.Text?.Trim();
        var username = UsernameEntry.Text?.Trim();
        var token = TokenEntry.Text?.Trim();
        var peer = PeerEntry.Text?.Trim();

        if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(peer))
        {
            Append("enter an App Key, a username, a user token and a peer username first");
            return;
        }

        LoginButton.IsEnabled = false;

        var client = ChatClient.Instance;

        // Harmless to call again on a client that is already initialised, which matters here since
        // ChatClient.Instance is a process-wide singleton and this page may run its login flow
        // more than once.
        //
        // Accepts either shape the Agora Console hands out: the legacy "org#appName" Chat App Key,
        // or the plain Agora App ID newer Chat projects are provisioned with — distinguished by the
        // "#" only the App Key form contains.
        var options = appKey.Contains('#')
            ? new ChatOptions { AppKey = appKey }
            : new ChatOptions { AppId = appKey };
        // global:: because this project's own namespace ends in ".Android", which would otherwise
        // shadow the Android.App namespace here.
        client.Init(global::Android.App.Application.Context, options);

        client.LoginWithToken(username, token, new Callback(
            onSuccess: () => MainThread.BeginInvokeOnMainThread(() =>
            {
                _peer = peer;
                client.ChatManager().MessageReceived += OnMessageReceived;
                Append($"logged in as {username}");
                SetLoggedIn(true);
            }),
            onError: (code, message) => MainThread.BeginInvokeOnMainThread(() =>
            {
                Append($"login failed ({code}): {message}");
                LoginButton.IsEnabled = true;
            })));
    }

    private void OnLogoutClicked(object sender, EventArgs e)
    {
        // The synchronous overload — Int32 Logout(bool) — is deliberately not used here: the
        // callback overload is what the rest of this page's flow follows, and it is what an app
        // with UI to update after logout actually wants.
        ChatClient.Instance.Logout(unbindToken: false, new Callback(
            onSuccess: () => MainThread.BeginInvokeOnMainThread(() =>
            {
                ChatClient.Instance.ChatManager().MessageReceived -= OnMessageReceived;
                _peer = null;
                Append("logged out");
                SetLoggedIn(false);
            }),
            onError: (code, message) => MainThread.BeginInvokeOnMainThread(() =>
                Append($"logout failed ({code}): {message}"))));
    }

    private void OnSendClicked(object sender, EventArgs e)
    {
        var text = MessageEntry.Text;
        if (_peer is null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // The factory sets From to the logged-in user and To to the username passed here.
        var message = ChatMessage.CreateTextSendMessage(text, _peer);
        message.SetMessageStatusCallback(new Callback(
            onSuccess: () => MainThread.BeginInvokeOnMainThread(() => Append($"you: {text}")),
            onError: (code, error) => MainThread.BeginInvokeOnMainThread(() =>
                Append($"send failed ({code}): {error}"))));

        ChatClient.Instance.ChatManager().SendMessage(message);
        MessageEntry.Text = "";
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        // P0 is exactly what the binding names it: the generator has no Java parameter name to
        // draw on for this single-argument callback.
        foreach (var message in e.P0)
        {
            var text = message.Body is TextMessageBody body ? body.Message : "[non-text]";
            MainThread.BeginInvokeOnMainThread(() => Append($"{message.From}: {text}"));
        }
    }

    private void SetLoggedIn(bool loggedIn)
    {
        LoginButton.IsEnabled = !loggedIn;
        LogoutButton.IsEnabled = loggedIn;
        MessageEntry.IsEnabled = loggedIn;
        SendButton.IsEnabled = loggedIn;
    }

    private void Append(string message)
    {
        _log.AppendLine($"{DateTime.Now:HH:mm:ss}  {message}");
        MessagesLabel.Text = _log.ToString();
        MessagesScroll.ScrollToAsync(0, MessagesLabel.Height, animated: false);
    }

    /// <summary>The SDK's own callback shape: three plain methods, called on the SDK's own thread.</summary>
    private sealed class Callback(Action onSuccess, Action<int, string> onError) : Java.Lang.Object, ICallBack
    {
        public void OnSuccess() => onSuccess();

        public void OnError(int code, string error) => onError(code, error);

        public void OnProgress(int progress, string status)
        {
            // Attachment upload progress; a text message never reports any.
        }
    }
}
