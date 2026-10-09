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
        using var settings = typeof(MauiProgram).Assembly.GetManifestResourceStream("Sensor3.UpdateSettings.json")
            ?? throw new InvalidOperationException("Uppdateringsinställningar saknas.");
        var updateOptions = System.Text.Json.JsonSerializer.Deserialize<Sensor3.Core.ApplicationUpdateOptions>(settings, Sensor3.Core.ApplicationUpdateService.JsonOptions)
            ?? throw new InvalidOperationException("Uppdateringsinställningar saknas.");
        builder.Services.AddSingleton(updateOptions);
        builder.Services.AddSingleton<Sensor3.Contracts.UpdateSessionGuard>();
        builder.Services.AddSingleton<Sensor3.Contracts.IUpdateSessionGuard>(x => x.GetRequiredService<Sensor3.Contracts.UpdateSessionGuard>());
        builder.Services.AddSingleton<Sensor3.Contracts.IUpdateInstaller, NativeUpdateInstaller>();
        builder.Services.AddSingleton<Sensor3.Contracts.IApplicationUpdateService>(x => new Sensor3.Core.ApplicationUpdateService(
            new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(5) }, updateOptions,
            new Sensor3.Contracts.InstalledApplication(AppInfo.Current.VersionString, long.Parse(AppInfo.Current.BuildString),
#if ANDROID
                Sensor3.Contracts.ClientPlatform.Android),
#else
                Sensor3.Contracts.ClientPlatform.Windows),
#endif
            x.GetRequiredService<Sensor3.Contracts.IUpdateInstaller>(), x.GetRequiredService<Sensor3.Contracts.IUpdateSessionGuard>(), Path.Combine(FileSystem.CacheDirectory, "updates")));
        builder.Services.AddSingleton(Sensor3.Core.BuildMetadata.Load(typeof(MauiProgram).Assembly));
        builder.Services.AddSingleton(Sensor3.Core.BuildMetadata.LoadIterations(typeof(MauiProgram).Assembly));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}


