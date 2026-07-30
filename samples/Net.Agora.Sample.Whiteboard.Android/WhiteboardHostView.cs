using Agora.Whiteboard;
using Microsoft.Maui.Handlers;

namespace Net.Agora.Sample.Whiteboard.Android;

/// <summary>
/// A MAUI view whose native counterpart <b>is</b> the board itself — a <see cref="WhiteboardView"/>
/// the app creates and hands to <see cref="WhiteSdk"/>'s constructor as its JS bridge. Same
/// technique the cross-platform façade's <c>Net.Agora.Whiteboard.Maui</c> package uses for
/// Android, reproduced here directly against the raw binding since this sample does not reference
/// that package.
/// </summary>
public class WhiteboardHostView : Microsoft.Maui.Controls.View
{
    /// <summary>
    /// The native board view, once this view has been realised — that is, after the page it is on
    /// has appeared. Null earlier, because the native view does not exist yet.
    /// </summary>
    public WhiteboardView? Board => Handler?.PlatformView as WhiteboardView;
}

public class WhiteboardHostViewHandler : ViewHandler<WhiteboardHostView, WhiteboardView>
{
    public static readonly IPropertyMapper<WhiteboardHostView, WhiteboardHostViewHandler> Mapper =
        new PropertyMapper<WhiteboardHostView, WhiteboardHostViewHandler>(ViewHandler.ViewMapper);

    public WhiteboardHostViewHandler() : base(Mapper)
    {
    }

    protected override WhiteboardView CreatePlatformView() => new(Context);
}
