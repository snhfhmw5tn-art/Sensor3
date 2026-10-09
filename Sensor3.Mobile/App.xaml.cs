namespace Sensor3;

public partial class App : Application
{
    private readonly Sensor3.Contracts.IApplicationUpdateService updates;
    private readonly Microsoft.Extensions.Logging.ILogger<App> logger;
    private readonly Sensor3.Contracts.ISensorProvider sensors;
    private readonly NativeObservationLifetime observations;
    public App(Sensor3.Contracts.IApplicationUpdateService updates, Microsoft.Extensions.Logging.ILogger<App> logger, Sensor3.Contracts.ISensorProvider sensors, NativeObservationLifetime observations)
    {
        this.updates = updates;
        this.logger = logger;
        this.sensors = sensors;
        this.observations = observations;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "Sensor3" };
        window.Stopped += async (_, _) => await StopSensorsAsync();
        window.Deactivated += async (_, _) => await StopSensorsAsync();
        window.Destroying += async (_, _) => await StopSensorsAsync();
        window.Created += async (_, _) =>
        {
            try { await updates.CheckAsync(); }
            catch (Exception exception) { Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(logger, exception, "Uppdateringskontroll vid start misslyckades"); }
        };
        return window;
    }
    private async Task StopSensorsAsync()
    {
        observations.CancelActive();
        try { await sensors.StopAsync(); }
        catch (Exception exception) { Microsoft.Extensions.Logging.LoggerExtensions.LogError(logger, exception, "Sensorinsamling kunde inte stoppas vid appavbrott"); }
    }
}

