using Microsoft.Extensions.Logging;

namespace Net.Agora.Sample.Whiteboard.Android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureMauiHandlers(handlers =>
                handlers.AddHandler<WhiteboardHostView, WhiteboardHostViewHandler>());

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
