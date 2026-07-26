// Hand-written addition to the generated Signaling binding. The generated Agora.Rtm.RtmClient
// already carries C# events for everything IRtmEventListener reports, but every *operation*
// (login, subscribe, publish, …) still completes through a Java ResultCallback — callback
// ceremony C# left behind with Task. This file adapts exactly those operations; the generated
// surface stays untouched underneath for anyone who wants the raw shape.

#nullable enable

using System;
using System.Threading.Tasks;

namespace Agora.Rtm
{
    /// <summary>
    /// A Signaling operation reported failure through its <c>ResultCallback.onFailure</c>. On
    /// Android that genuinely means failure: unlike the iOS Signaling SDK — where the delegate
    /// hands over an <c>errorInfo</c> whose code 0 accompanies success and must be filtered —
    /// the Android SDK calls <c>onFailure</c> only for real failures, so the Task adapters below
    /// fault, and catching this exception is the complete error-handling story.
    /// </summary>
    public sealed class RtmOperationException : Exception
    {
        /// <summary>Creates the exception from the SDK's failure report.</summary>
        /// <param name="errorInfo">
        /// What the SDK handed to <c>onFailure</c>. Nullable because the callback's parameter
        /// is: a defensive null still produces a throwable exception rather than a second fault.
        /// </param>
        public RtmOperationException(ErrorInfo? errorInfo)
            : base(MessageFor(errorInfo))
        {
            ErrorInfo = errorInfo;
        }

        /// <summary>
        /// The SDK's full failure report: <c>ErrorCode</c> (translate to a number with
        /// <c>RtmConstants.RtmErrorCode.GetValue</c>), <c>ErrorReason</c> and <c>Operation</c>.
        /// </summary>
        public ErrorInfo? ErrorInfo { get; }

        private static string MessageFor(ErrorInfo? errorInfo) =>
            errorInfo is null
                ? "Agora Signaling operation failed (the SDK reported no ErrorInfo)."
                : $"Agora Signaling operation '{errorInfo.Operation}' failed: " +
                  $"{errorInfo.ErrorReason} ({errorInfo.ErrorCode})";
    }

    /// <summary>
    /// Awaitable adapters over the generated <see cref="RtmClient"/> operations. Each method is
    /// the same native call the callback-taking overload makes — nothing is retried, queued or
    /// reinterpreted; the Task completes when the SDK's <c>ResultCallback</c> fires and faults
    /// with <see cref="RtmOperationException"/> when it fires <c>onFailure</c>.
    /// </summary>
    public static class RtmClientAsyncExtensions
    {
        /// <summary>
        /// Logs in to the Signaling service — <c>RtmClient.login</c> as a Task.
        /// </summary>
        /// <param name="client">The client to log in.</param>
        /// <param name="token">
        /// The token, or the App ID itself for an App ID-only (no token) project. Nullable
        /// because the underlying Java parameter is.
        /// </param>
        /// <exception cref="RtmOperationException">The SDK reported failure.</exception>
        public static Task LoginAsync(this RtmClient client, string? token)
        {
            ArgumentNullException.ThrowIfNull(client);
            var callback = new TaskResultCallback();
            client.Login(token, callback);
            return callback.Task;
        }

        /// <summary>
        /// Logs out of the Signaling service — <c>RtmClient.logout</c> as a Task.
        /// </summary>
        /// <param name="client">The client to log out.</param>
        /// <exception cref="RtmOperationException">The SDK reported failure.</exception>
        public static Task LogoutAsync(this RtmClient client)
        {
            ArgumentNullException.ThrowIfNull(client);
            var callback = new TaskResultCallback();
            client.Logout(callback);
            return callback.Task;
        }

        /// <summary>
        /// Replaces the current token before it expires — <c>RtmClient.renewToken</c> as a
        /// Task. Call it from the <see cref="RtmClient.TokenPrivilegeWillExpire"/> event.
        /// </summary>
        /// <param name="client">The logged-in client.</param>
        /// <param name="token">The fresh token.</param>
        /// <exception cref="RtmOperationException">The SDK reported failure.</exception>
        public static Task RenewTokenAsync(this RtmClient client, string token)
        {
            ArgumentNullException.ThrowIfNull(client);
            var callback = new TaskResultCallback();
            client.RenewToken(token, callback);
            return callback.Task;
        }

        /// <summary>
        /// Subscribes to a message channel — <c>RtmClient.subscribe</c> as a Task. Messages
        /// then arrive through the <see cref="RtmClient.MessageEvent"/> event.
        /// </summary>
        /// <param name="client">The logged-in client.</param>
        /// <param name="channel">The channel name.</param>
        /// <param name="options">
        /// What to subscribe to beyond messages (presence, metadata, locks); omit for the SDK's
        /// defaults. The Java overload has no options-free form, so a fresh default
        /// <see cref="SubscribeOptions"/> is passed when this is null.
        /// </param>
        /// <exception cref="RtmOperationException">The SDK reported failure.</exception>
        public static Task SubscribeAsync(this RtmClient client, string channel, SubscribeOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(client);
            var callback = new TaskResultCallback();
            client.Subscribe(channel, options ?? new SubscribeOptions(), callback);
            return callback.Task;
        }

        /// <summary>
        /// Unsubscribes from a message channel — <c>RtmClient.unsubscribe</c> as a Task.
        /// </summary>
        /// <param name="client">The logged-in client.</param>
        /// <param name="channel">The channel name.</param>
        /// <exception cref="RtmOperationException">The SDK reported failure.</exception>
        public static Task UnsubscribeAsync(this RtmClient client, string channel)
        {
            ArgumentNullException.ThrowIfNull(client);
            var callback = new TaskResultCallback();
            client.Unsubscribe(channel, callback);
            return callback.Task;
        }

        /// <summary>
        /// Publishes a string message to a channel — <c>RtmClient.publish</c> as a Task. The
        /// publisher does not receive its own message back through
        /// <see cref="RtmClient.MessageEvent"/>; completion of this Task is the delivery report.
        /// </summary>
        /// <param name="client">The logged-in client.</param>
        /// <param name="channel">The channel name.</param>
        /// <param name="message">The message text.</param>
        /// <param name="options">
        /// Delivery options (custom type, storage); omit for the SDK's defaults. The Java
        /// overload has no options-free form, so a fresh default <see cref="PublishOptions"/> is
        /// passed when this is null.
        /// </param>
        /// <exception cref="RtmOperationException">The SDK reported failure — including error
        /// code -10025 (<c>NOT_LOGIN</c>) when publishing before login.</exception>
        public static Task PublishAsync(this RtmClient client, string channel, string message, PublishOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(client);
            var callback = new TaskResultCallback();
            client.Publish(channel, message, options ?? new PublishOptions(), callback);
            return callback.Task;
        }
    }

    /// <summary>
    /// One operation's ResultCallback, bridged to a TaskCompletionSource. The SDK fires the
    /// callback on its own thread; RunContinuationsAsynchronously keeps awaiting continuations
    /// off that thread, so nothing an app does after <c>await</c> can block the SDK's callback
    /// loop. No extra rooting is needed against premature collection: the Java side holds the
    /// callback's peer until it fires, and that peer holds this managed object.
    /// </summary>
    internal sealed class TaskResultCallback : Java.Lang.Object, IResultCallback
    {
        private readonly TaskCompletionSource _completion =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public System.Threading.Tasks.Task Task => _completion.Task;

        public void OnSuccess(Java.Lang.Object? responseInfo) => _completion.TrySetResult();

        public void OnFailure(ErrorInfo? errorInfo) =>
            _completion.TrySetException(new RtmOperationException(errorInfo));
    }
}
