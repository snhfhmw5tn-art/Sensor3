namespace Sensor3;

public partial class App : Application
{
    private readonly Sensor3.Contracts.IApplicationUpdateService updates;
    private readonly Microsoft.Extensions.Logging.ILogger<App> logger;
    public App(Sensor3.Contracts.IApplicationUpdateService updates, Microsoft.Extensions.Logging.ILogger<App> logger)
    {
        this.updates = updates;
        this.logger = logger;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "Sensor3" };
        window.Created += async (_, _) =>
        {
            try { await updates.CheckAsync(); }
            catch (Exception exception) { Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(logger, exception, "Uppdateringskontroll vid start misslyckades"); }
        };
        return window;
    }
}

