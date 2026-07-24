using Microsoft.Extensions.Logging;

namespace Net.Agora.Sample.Voice.Android;

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
        // No handlers to register: voice renders nothing — that is the point of the package.

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
