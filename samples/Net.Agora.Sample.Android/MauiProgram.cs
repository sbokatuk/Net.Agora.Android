using Microsoft.Extensions.Logging;

namespace Net.Agora.Sample.Android;

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
            // The view Agora renders the camera preview into — see AgoraVideoView.cs for why a
            // custom handler rather than a wrapped ContentView.
            .ConfigureMauiHandlers(handlers =>
                handlers.AddHandler<AgoraVideoView, AgoraVideoViewHandler>());

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
