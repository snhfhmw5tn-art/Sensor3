using Microsoft.Extensions.Logging;

namespace Sensor3;

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
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton(Sensor3.Core.BuildMetadata.Load(typeof(MauiProgram).Assembly));
        builder.Services.AddSingleton(Sensor3.Core.BuildMetadata.LoadIterations(typeof(MauiProgram).Assembly));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}


