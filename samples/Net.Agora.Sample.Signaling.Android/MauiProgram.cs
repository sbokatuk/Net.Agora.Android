using Microsoft.Extensions.Logging;

namespace Net.Agora.Sample.Signaling.Android;

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
            });
        // No handlers to register: RTM renders nothing and needs no platform glue.

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
