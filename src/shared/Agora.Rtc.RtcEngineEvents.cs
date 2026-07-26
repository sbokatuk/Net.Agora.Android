// Hand-written addition compiled into BOTH RTC binding projects — Net.Agora.Video.Android and
// Net.Agora.Voice.Android reference this one file (<Compile Include="../shared/..." /> in each
// .csproj). The two packages bind the same io.agora.rtc2 Java API layer into the same Agora.Rtc
// namespace and are mutually exclusive within one app (same Java classes, dex merge fails), so
// the surface must exist identically in both and can never collide at runtime.
//
// Why this exists: the generated binding exposes Agora's callback surface the Java way — an
// IRtcEngineEventHandler subclass, overridden per app and registered through
// RtcEngine.AddHandler. C# consumers expect events. This file adds them for the callbacks an app
// subscribes first, without touching the generated code: RtcEngine is generated partial, so the
// events live in this second half of the class.
//
// Lifetime: the first subscription to any event creates one private EventDispatcher (an
// IRtcEngineEventHandler) and registers it via AddHandler. It is deliberately never removed and
// held in an instance field — that field is what keeps the managed dispatcher (and through it the
// subscribed delegates) alive for as long as the engine wrapper lives, closing the classic Java
// binding trap where a listener with no managed root is collected and callbacks silently stop.
// AddHandler is re-invoked on every subscription rather than only the first: the Java side keys
// handlers by identity (RtcEngineImpl.addHandler is mRtcHandlers.put(handler, proxy) — verified
// against 4.6.3 bytecode), so re-adding the same dispatcher can never duplicate it, and it heals
// the one hole a subscribe-once flag would leave — a second RtcEngine.Create without Destroy
// reinitializes the same singleton and clears every handler added this way.

#nullable enable

using System;

namespace Agora.Rtc
{
    public partial class RtcEngine
    {
        /// <summary>
        /// Guards dispatcher creation and delegate mutation. Subscription can race between UI
        /// code and SDK callbacks; the raise path deliberately does not take this lock (it reads
        /// one delegate field), so an event can never deadlock against a subscription.
        /// </summary>
        private readonly object _eventGate = new object();

        private EventDispatcher? _eventDispatcher;

        private EventHandler<RtcJoinChannelSuccessEventArgs>? _joinChannelSuccess;
        private EventHandler<RtcLeftChannelEventArgs>? _leftChannel;
        private EventHandler<RtcUserJoinedEventArgs>? _userJoined;
        private EventHandler<RtcUserOfflineEventArgs>? _userOffline;
        private EventHandler<RtcErrorEventArgs>? _error;
        private EventHandler<RtcUserMuteAudioEventArgs>? _userMuteAudio;
        private EventHandler<RtcUserMuteVideoEventArgs>? _userMuteVideo;
        private EventHandler<RtcAudioVolumeIndicationEventArgs>? _audioVolumeIndication;
        private EventHandler<RtcTokenPrivilegeWillExpireEventArgs>? _tokenPrivilegeWillExpire;
        private EventHandler<RtcConnectionStateChangedEventArgs>? _connectionStateChanged;

        /// <summary>
        /// Raised when the local user has joined a channel. Fed by the native
        /// <c>IRtcEngineEventHandler.onJoinChannelSuccess</c> callback.
        /// </summary>
        /// <remarks>
        /// The SDK raises this on one of its own worker threads, never the UI thread — marshal
        /// any UI work back (for example <c>MainThread.BeginInvokeOnMainThread</c> in MAUI)
        /// before touching views.
        /// </remarks>
        public event EventHandler<RtcJoinChannelSuccessEventArgs> JoinChannelSuccess
        {
            add { lock (_eventGate) { InstallEventDispatcher(); _joinChannelSuccess += value; } }
            remove { lock (_eventGate) { _joinChannelSuccess -= value; } }
        }

        /// <summary>
        /// Raised when the local user has left the channel, carrying the session's final
        /// statistics. Fed by the native <c>IRtcEngineEventHandler.onLeaveChannel</c> callback.
        /// Named <c>LeftChannel</c> rather than <c>LeaveChannel</c> because
        /// <see cref="RtcEngine"/> already has the <c>LeaveChannel()</c> method and C# forbids a
        /// method and an event sharing a name — and the past tense reads correctly anyway: this
        /// reports that the channel <em>was</em> left.
        /// </summary>
        /// <remarks>
        /// The SDK raises this on one of its own worker threads, never the UI thread — marshal
        /// any UI work back (for example <c>MainThread.BeginInvokeOnMainThread</c> in MAUI)
        /// before touching views.
        /// </remarks>
        public event EventHandler<RtcLeftChannelEventArgs> LeftChannel
        {
            add { lock (_eventGate) { InstallEventDispatcher(); _leftChannel += value; } }
            remove { lock (_eventGate) { _leftChannel -= value; } }
        }

        /// <summary>
        /// Raised when a remote user or host joins the channel. Fed by the native
        /// <c>IRtcEngineEventHandler.onUserJoined</c> callback.
        /// </summary>
        /// <remarks>
        /// The SDK raises this on one of its own worker threads, never the UI thread — marshal
        /// any UI work back (for example <c>MainThread.BeginInvokeOnMainThread</c> in MAUI)
        /// before touching views.
        /// </remarks>
        public event EventHandler<RtcUserJoinedEventArgs> UserJoined
        {
            add { lock (_eventGate) { InstallEventDispatcher(); _userJoined += value; } }
            remove { lock (_eventGate) { _userJoined -= value; } }
        }

        /// <summary>
        /// Raised when a remote user leaves the channel or drops offline. Fed by the native
        /// <c>IRtcEngineEventHandler.onUserOffline</c> callback.
        /// </summary>
        /// <remarks>
        /// The SDK raises this on one of its own worker threads, never the UI thread — marshal
        /// any UI work back (for example <c>MainThread.BeginInvokeOnMainThread</c> in MAUI)
        /// before touching views.
        /// </remarks>
        public event EventHandler<RtcUserOfflineEventArgs> UserOffline
        {
            add { lock (_eventGate) { InstallEventDispatcher(); _userOffline += value; } }
            remove { lock (_eventGate) { _userOffline -= value; } }
        }

        /// <summary>
        /// Raised when the SDK reports an error during runtime. Fed by the native
        /// <c>IRtcEngineEventHandler.onError</c> callback;
        /// <see cref="GetErrorDescription(int)"/> translates the code.
        /// </summary>
        /// <remarks>
        /// The SDK raises this on one of its own worker threads, never the UI thread — marshal
        /// any UI work back (for example <c>MainThread.BeginInvokeOnMainThread</c> in MAUI)
        /// before touching views.
        /// </remarks>
        public event EventHandler<RtcErrorEventArgs> Error
        {
            add { lock (_eventGate) { InstallEventDispatcher(); _error += value; } }
            remove { lock (_eventGate) { _error -= value; } }
        }

        /// <summary>
        /// Raised when a remote user mutes or unmutes their audio stream. Fed by the native
        /// <c>IRtcEngineEventHandler.onUserMuteAudio</c> callback.
        /// </summary>
        /// <remarks>
        /// The SDK raises this on one of its own worker threads, never the UI thread — marshal
        /// any UI work back (for example <c>MainThread.BeginInvokeOnMainThread</c> in MAUI)
        /// before touching views.
        /// </remarks>
        public event EventHandler<RtcUserMuteAudioEventArgs> UserMuteAudio
        {
            add { lock (_eventGate) { InstallEventDispatcher(); _userMuteAudio += value; } }
            remove { lock (_eventGate) { _userMuteAudio -= value; } }
        }

        /// <summary>
        /// Raised when a remote user mutes or unmutes their video stream. Fed by the native
        /// <c>IRtcEngineEventHandler.onUserMuteVideo</c> callback. Present in the Voice binding
        /// too: the voice .aar ships the same Java API layer, video pipeline stripped.
        /// </summary>
        /// <remarks>
        /// The SDK raises this on one of its own worker threads, never the UI thread — marshal
        /// any UI work back (for example <c>MainThread.BeginInvokeOnMainThread</c> in MAUI)
        /// before touching views.
        /// </remarks>
        public event EventHandler<RtcUserMuteVideoEventArgs> UserMuteVideo
        {
            add { lock (_eventGate) { InstallEventDispatcher(); _userMuteVideo += value; } }
            remove { lock (_eventGate) { _userMuteVideo -= value; } }
        }

        /// <summary>
        /// Raised on the who-is-speaking cadence enabled by
        /// <c>EnableAudioVolumeIndication</c>. Fed by the native
        /// <c>IRtcEngineEventHandler.onAudioVolumeIndication</c> callback.
        /// </summary>
        /// <remarks>
        /// The SDK raises this on one of its own worker threads, never the UI thread — marshal
        /// any UI work back (for example <c>MainThread.BeginInvokeOnMainThread</c> in MAUI)
        /// before touching views.
        /// </remarks>
        public event EventHandler<RtcAudioVolumeIndicationEventArgs> AudioVolumeIndication
        {
            add { lock (_eventGate) { InstallEventDispatcher(); _audioVolumeIndication += value; } }
            remove { lock (_eventGate) { _audioVolumeIndication -= value; } }
        }

        /// <summary>
        /// Raised roughly 30 seconds before the current token expires — the cue to fetch a fresh
        /// token and call <c>RenewToken</c>. Fed by the native
        /// <c>IRtcEngineEventHandler.onTokenPrivilegeWillExpire</c> callback.
        /// </summary>
        /// <remarks>
        /// The SDK raises this on one of its own worker threads, never the UI thread — marshal
        /// any UI work back (for example <c>MainThread.BeginInvokeOnMainThread</c> in MAUI)
        /// before touching views.
        /// </remarks>
        public event EventHandler<RtcTokenPrivilegeWillExpireEventArgs> TokenPrivilegeWillExpire
        {
            add { lock (_eventGate) { InstallEventDispatcher(); _tokenPrivilegeWillExpire += value; } }
            remove { lock (_eventGate) { _tokenPrivilegeWillExpire -= value; } }
        }

        /// <summary>
        /// Raised when the connection to Agora's edge changes state (connecting, connected,
        /// reconnecting, failed…). Fed by the native
        /// <c>IRtcEngineEventHandler.onConnectionStateChanged</c> callback; the raw codes are
        /// the <c>Constants.CONNECTION_STATE_*</c> / <c>CONNECTION_CHANGED_*</c> values.
        /// </summary>
        /// <remarks>
        /// The SDK raises this on one of its own worker threads, never the UI thread — marshal
        /// any UI work back (for example <c>MainThread.BeginInvokeOnMainThread</c> in MAUI)
        /// before touching views.
        /// </remarks>
        public event EventHandler<RtcConnectionStateChangedEventArgs> ConnectionStateChanged
        {
            add { lock (_eventGate) { InstallEventDispatcher(); _connectionStateChanged += value; } }
            remove { lock (_eventGate) { _connectionStateChanged -= value; } }
        }

        /// <summary>
        /// Creates the dispatcher on the first subscription and (re)registers it. Always called
        /// under <see cref="_eventGate"/>. See the file header for why AddHandler runs on every
        /// subscription rather than once: it is identity-keyed on the Java side, so this can
        /// never install a duplicate, and it survives an engine reinitialize clearing the
        /// handler table.
        /// </summary>
        private void InstallEventDispatcher()
        {
            _eventDispatcher ??= new EventDispatcher(this);
            AddHandler(_eventDispatcher);
        }

        /// <summary>
        /// The one handler behind every event on this engine. Raises straight through on the
        /// SDK's callback thread — marshalling is the subscriber's decision, not this class's,
        /// because a dispatcher that hopped to the UI thread would reorder events against direct
        /// <see cref="IRtcEngineEventHandler"/> subclasses and hide the threading cost.
        /// </summary>
        private sealed class EventDispatcher : IRtcEngineEventHandler
        {
            private readonly RtcEngine _engine;

            internal EventDispatcher(RtcEngine engine) => _engine = engine;

            public override void OnJoinChannelSuccess(string? channel, int uid, int elapsed) =>
                _engine._joinChannelSuccess?.Invoke(_engine, new RtcJoinChannelSuccessEventArgs(channel, uid, elapsed));

            public override void OnLeaveChannel(IRtcEngineEventHandler.RtcStats? stats) =>
                _engine._leftChannel?.Invoke(_engine, new RtcLeftChannelEventArgs(stats));

            public override void OnUserJoined(int uid, int elapsed) =>
                _engine._userJoined?.Invoke(_engine, new RtcUserJoinedEventArgs(uid, elapsed));

            public override void OnUserOffline(int uid, int reason) =>
                _engine._userOffline?.Invoke(_engine, new RtcUserOfflineEventArgs(uid, reason));

            public override void OnError(int err) =>
                _engine._error?.Invoke(_engine, new RtcErrorEventArgs(err));

            public override void OnUserMuteAudio(int uid, bool muted) =>
                _engine._userMuteAudio?.Invoke(_engine, new RtcUserMuteAudioEventArgs(uid, muted));

            public override void OnUserMuteVideo(int uid, bool muted) =>
                _engine._userMuteVideo?.Invoke(_engine, new RtcUserMuteVideoEventArgs(uid, muted));

            public override void OnAudioVolumeIndication(IRtcEngineEventHandler.AudioVolumeInfo[]? speakers, int totalVolume) =>
                _engine._audioVolumeIndication?.Invoke(_engine, new RtcAudioVolumeIndicationEventArgs(speakers, totalVolume));

            public override void OnTokenPrivilegeWillExpire(string? token) =>
                _engine._tokenPrivilegeWillExpire?.Invoke(_engine, new RtcTokenPrivilegeWillExpireEventArgs(token));

            public override void OnConnectionStateChanged(int state, int reason) =>
                _engine._connectionStateChanged?.Invoke(_engine, new RtcConnectionStateChangedEventArgs(state, reason));
        }
    }

    /// <summary>
    /// Data for <see cref="RtcEngine.JoinChannelSuccess"/> — the native
    /// <c>onJoinChannelSuccess</c> parameters, unchanged.
    /// </summary>
    public sealed class RtcJoinChannelSuccessEventArgs : EventArgs
    {
        /// <summary>Creates the arguments; called by the engine's event dispatcher.</summary>
        public RtcJoinChannelSuccessEventArgs(string? channel, int uid, int elapsed)
        {
            Channel = channel;
            Uid = uid;
            Elapsed = elapsed;
        }

        /// <summary>The channel name that was joined.</summary>
        public string? Channel { get; }

        /// <summary>The local user id the server assigned (or the one requested).</summary>
        public int Uid { get; }

        /// <summary>Milliseconds from calling <c>JoinChannel</c> to this callback.</summary>
        public int Elapsed { get; }
    }

    /// <summary>
    /// Data for <see cref="RtcEngine.LeftChannel"/> — the native <c>onLeaveChannel</c>
    /// parameter, unchanged.
    /// </summary>
    public sealed class RtcLeftChannelEventArgs : EventArgs
    {
        /// <summary>Creates the arguments; called by the engine's event dispatcher.</summary>
        public RtcLeftChannelEventArgs(IRtcEngineEventHandler.RtcStats? stats) => Stats = stats;

        /// <summary>The whole session's statistics, as the SDK reports them on leave.</summary>
        public IRtcEngineEventHandler.RtcStats? Stats { get; }
    }

    /// <summary>
    /// Data for <see cref="RtcEngine.UserJoined"/> — the native <c>onUserJoined</c> parameters,
    /// unchanged.
    /// </summary>
    public sealed class RtcUserJoinedEventArgs : EventArgs
    {
        /// <summary>Creates the arguments; called by the engine's event dispatcher.</summary>
        public RtcUserJoinedEventArgs(int uid, int elapsed)
        {
            Uid = uid;
            Elapsed = elapsed;
        }

        /// <summary>The remote user's id.</summary>
        public int Uid { get; }

        /// <summary>Milliseconds from the local <c>JoinChannel</c> to this callback.</summary>
        public int Elapsed { get; }
    }

    /// <summary>
    /// Data for <see cref="RtcEngine.UserOffline"/> — the native <c>onUserOffline</c>
    /// parameters, unchanged.
    /// </summary>
    public sealed class RtcUserOfflineEventArgs : EventArgs
    {
        /// <summary>Creates the arguments; called by the engine's event dispatcher.</summary>
        public RtcUserOfflineEventArgs(int uid, int reason)
        {
            Uid = uid;
            Reason = reason;
        }

        /// <summary>The remote user's id.</summary>
        public int Uid { get; }

        /// <summary>
        /// Why they went offline — a <c>Constants.USER_OFFLINE_*</c> code (quit, dropped, or
        /// became audience).
        /// </summary>
        public int Reason { get; }
    }

    /// <summary>
    /// Data for <see cref="RtcEngine.Error"/> — the native <c>onError</c> parameter, unchanged.
    /// </summary>
    public sealed class RtcErrorEventArgs : EventArgs
    {
        /// <summary>Creates the arguments; called by the engine's event dispatcher.</summary>
        public RtcErrorEventArgs(int code) => Code = code;

        /// <summary>
        /// The Agora error code; <see cref="RtcEngine.GetErrorDescription(int)"/> translates it
        /// to text.
        /// </summary>
        public int Code { get; }
    }

    /// <summary>
    /// Data for <see cref="RtcEngine.UserMuteAudio"/> — the native <c>onUserMuteAudio</c>
    /// parameters, unchanged.
    /// </summary>
    public sealed class RtcUserMuteAudioEventArgs : EventArgs
    {
        /// <summary>Creates the arguments; called by the engine's event dispatcher.</summary>
        public RtcUserMuteAudioEventArgs(int uid, bool muted)
        {
            Uid = uid;
            Muted = muted;
        }

        /// <summary>The remote user's id.</summary>
        public int Uid { get; }

        /// <summary><c>true</c> when they muted, <c>false</c> when they unmuted.</summary>
        public bool Muted { get; }
    }

    /// <summary>
    /// Data for <see cref="RtcEngine.UserMuteVideo"/> — the native <c>onUserMuteVideo</c>
    /// parameters, unchanged.
    /// </summary>
    public sealed class RtcUserMuteVideoEventArgs : EventArgs
    {
        /// <summary>Creates the arguments; called by the engine's event dispatcher.</summary>
        public RtcUserMuteVideoEventArgs(int uid, bool muted)
        {
            Uid = uid;
            Muted = muted;
        }

        /// <summary>The remote user's id.</summary>
        public int Uid { get; }

        /// <summary><c>true</c> when they muted, <c>false</c> when they unmuted.</summary>
        public bool Muted { get; }
    }

    /// <summary>
    /// Data for <see cref="RtcEngine.AudioVolumeIndication"/> — the native
    /// <c>onAudioVolumeIndication</c> parameters, unchanged.
    /// </summary>
    public sealed class RtcAudioVolumeIndicationEventArgs : EventArgs
    {
        /// <summary>Creates the arguments; called by the engine's event dispatcher.</summary>
        public RtcAudioVolumeIndicationEventArgs(IRtcEngineEventHandler.AudioVolumeInfo[]? speakers, int totalVolume)
        {
            Speakers = speakers;
            TotalVolume = totalVolume;
        }

        /// <summary>
        /// Who is speaking and how loudly; uid 0 is the local user. The SDK's own array,
        /// not a copy — treat it as read-only.
        /// </summary>
        public IRtcEngineEventHandler.AudioVolumeInfo[]? Speakers { get; }

        /// <summary>The mixed volume after all speakers, 0–255.</summary>
        public int TotalVolume { get; }
    }

    /// <summary>
    /// Data for <see cref="RtcEngine.TokenPrivilegeWillExpire"/> — the native
    /// <c>onTokenPrivilegeWillExpire</c> parameter, unchanged.
    /// </summary>
    public sealed class RtcTokenPrivilegeWillExpireEventArgs : EventArgs
    {
        /// <summary>Creates the arguments; called by the engine's event dispatcher.</summary>
        public RtcTokenPrivilegeWillExpireEventArgs(string? token) => Token = token;

        /// <summary>The token that is about to expire.</summary>
        public string? Token { get; }
    }

    /// <summary>
    /// Data for <see cref="RtcEngine.ConnectionStateChanged"/> — the native
    /// <c>onConnectionStateChanged</c> parameters, unchanged.
    /// </summary>
    public sealed class RtcConnectionStateChangedEventArgs : EventArgs
    {
        /// <summary>Creates the arguments; called by the engine's event dispatcher.</summary>
        public RtcConnectionStateChangedEventArgs(int state, int reason)
        {
            State = state;
            Reason = reason;
        }

        /// <summary>The new connection state — a <c>Constants.CONNECTION_STATE_*</c> code.</summary>
        public int State { get; }

        /// <summary>Why it changed — a <c>Constants.CONNECTION_CHANGED_*</c> code.</summary>
        public int Reason { get; }
    }
}
