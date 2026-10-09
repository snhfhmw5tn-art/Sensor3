using Microsoft.Extensions.Logging;

namespace Sensor3;

public static class MauiProgram
{
    private static string InitializeDeviceId()
    {
        var id = Preferences.Default.Get("TelemetryDeviceId", "");
        if (id.Length == 0) { id = Guid.NewGuid().ToString("N"); Preferences.Default.Set("TelemetryDeviceId", id); }
        return id;
    }
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
        builder.Services.AddSingleton<NativeObservationLifetime>();
        builder.Services.AddSingleton<Sensor3.Contracts.ILocationProvider, NativeLocationProvider>();
        builder.Services.AddSingleton<Sensor3.Contracts.IStepSensorProvider, Sensor3.Sensors.NativeStepSensorProvider>();
#if ANDROID
        builder.Services.AddSingleton<AndroidRadioScanner>();
        builder.Services.AddSingleton<Sensor3.Contracts.IWifiScanner>(x => x.GetRequiredService<AndroidRadioScanner>());
        builder.Services.AddSingleton<Sensor3.Contracts.IBluetoothScanner>(x => x.GetRequiredService<AndroidRadioScanner>());
        builder.Services.AddSingleton<Sensor3.Sensors.ISensorBackend, AndroidSensorBackend>();
        builder.Services.AddSingleton<AndroidSensorPermissions>();
        builder.Services.AddSingleton<Sensor3.Contracts.ISensorPermissionService>(x => x.GetRequiredService<AndroidSensorPermissions>());
#else
        builder.Services.AddSingleton<WindowsRadioScanner>();
        builder.Services.AddSingleton<Sensor3.Contracts.IWifiScanner>(x => x.GetRequiredService<WindowsRadioScanner>());
        builder.Services.AddSingleton<Sensor3.Contracts.IBluetoothScanner>(x => x.GetRequiredService<WindowsRadioScanner>());
        builder.Services.AddSingleton<Sensor3.Sensors.ISensorBackend, WindowsSensorBackend>();
#endif
        builder.Services.AddSingleton<Sensor3.Contracts.ISensorProvider, Sensor3.Sensors.SensorProvider>();
        builder.Services.AddSingleton<Sensor3.Core.StepSession>();
        builder.Services.AddSingleton<Sensor3.Contracts.IStepSession>(x => x.GetRequiredService<Sensor3.Core.StepSession>());
        builder.Services.AddSingleton<Sensor3.Contracts.ISensorFusionSession>(x => x.GetRequiredService<Sensor3.Core.StepSession>());
        builder.Services.AddSingleton<Sensor3.Contracts.IActivitySession>(x => x.GetRequiredService<Sensor3.Core.StepSession>());
        builder.Services.AddSingleton<Sensor3.Sensors.SensorDiagnosticsStore>();
        builder.Services.AddSingleton<Sensor3.Contracts.ISensorDiagnosticsReceiver>(x => x.GetRequiredService<Sensor3.Sensors.SensorDiagnosticsStore>());
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

        builder.Services.AddSingleton<Sensor3.Contracts.ITelemetryClient>(x => new Sensor3.Sensors.SensorTelemetryClient(
            Preferences.Default.Get("TelemetryDeviceId", InitializeDeviceId()),
            x.GetRequiredService<Sensor3.Contracts.ISensorProvider>(), x.GetRequiredService<Sensor3.Sensors.SensorDiagnosticsStore>(),
            x.GetRequiredService<Sensor3.Contracts.BuildInfo>(), x.GetRequiredService<IReadOnlyList<Sensor3.Contracts.IterationInfo>>(),
            x.GetRequiredService<ILogger<Sensor3.Sensors.SensorTelemetryClient>>()));
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}


