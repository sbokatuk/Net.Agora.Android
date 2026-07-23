using Android.Views;
using Microsoft.Maui.Handlers;

namespace Net.Agora.Sample.Android;

/// <summary>
/// A MAUI view that Agora renders video into — a plain <c>SurfaceView</c>, which is what Agora's
/// own quickstart samples pass to <c>VideoCanvas</c>; there is no SDK-provided view subclass to
/// instantiate instead.
///
/// A custom view rather than a wrapped <c>ContentView</c> because <c>VideoCanvas</c> wants the
/// native view itself, not a MAUI wrapper around it. This is the same pattern as
/// Net.Agora.Video.Maui's AgoraVideoView in the façade repository, minus the iOS half — this
/// sample is Android-only, so no <c>#if</c> is needed.
/// </summary>
// Explicitly Microsoft.Maui.Controls.View: Android.Views.View is also in scope (for the handler
// below) and "View" alone is ambiguous between the two.
public class AgoraVideoView : Microsoft.Maui.Controls.View
{
}

/// <summary>Binds <see cref="AgoraVideoView"/> to a native <see cref="SurfaceView"/>.</summary>
public class AgoraVideoViewHandler : ViewHandler<AgoraVideoView, SurfaceView>
{
    /// <summary>The view has no properties of its own; the mapper exists because a handler needs one.</summary>
    public static readonly IPropertyMapper<AgoraVideoView, AgoraVideoViewHandler> VideoMapper =
        new PropertyMapper<AgoraVideoView, AgoraVideoViewHandler>(ViewMapper);

    /// <summary>Creates the handler. Registered in <see cref="MauiProgram.CreateMauiApp"/>.</summary>
    public AgoraVideoViewHandler() : base(VideoMapper)
    {
    }

    /// <inheritdoc />
    protected override SurfaceView CreatePlatformView() => new(Context);
}
