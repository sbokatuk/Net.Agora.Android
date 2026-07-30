using Agora.Fastboard;
using Microsoft.Maui.Handlers;

namespace Net.Agora.Sample.Fastboard.Android;

/// <summary>
/// A MAUI view whose native counterpart <b>is</b> the board and its toolbar. Unlike iOS's
/// Fastboard binding — where the SDK builds its own view once the client is constructed — the
/// Android SDK takes a <see cref="FastboardView"/> the app supplies, so the handler creates one
/// directly and <see cref="Board"/> hands it to <c>Agora.Fastboard.Fastboard</c>'s
/// constructor. Same technique the cross-platform façade's <c>Net.Agora.Fastboard.Maui</c>
/// package uses for Android, reproduced here directly against the raw binding since this sample
/// does not reference that package.
/// </summary>
public class FastboardHostView : Microsoft.Maui.Controls.View
{
    /// <summary>
    /// The native board view, once this view has been realised — that is, after the page it is on
    /// has appeared. Null earlier, because the native view does not exist yet.
    /// </summary>
    public FastboardView? Board => Handler?.PlatformView as FastboardView;
}

public class FastboardHostViewHandler : ViewHandler<FastboardHostView, FastboardView>
{
    public static readonly IPropertyMapper<FastboardHostView, FastboardHostViewHandler> Mapper =
        new PropertyMapper<FastboardHostView, FastboardHostViewHandler>(ViewHandler.ViewMapper);

    public FastboardHostViewHandler() : base(Mapper)
    {
    }

    protected override FastboardView CreatePlatformView() => new(Context);
}
