using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sensor3.Contracts;
using Sensor3.Sensors;
using Sensor3.SharedUI.Components;

namespace Sensor3.Tests;

[TestClass]
public sealed class SensorDiagnosticsRenderingTests
{
    private static async Task<string> RenderAsync(SensorDiagnosticsStore store)
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton(store);
        services.AddSingleton(new BuildInfo { ApplicationVersion = "server-version", GitCommitHash = "server-commit" });
        await using var scope = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(scope, scope.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<SensorDiagnosticsView>(ParameterView.Empty);
            return component.ToHtmlString();
        });
    }
    [TestMethod]
    public async Task TestThat_browser_without_native_data_renders_unknown_client_without_controls_or_graphs()
    {
        var html = await RenderAsync(new());
        StringAssert.Contains(html, "Klientversion: Unknown");
        StringAssert.Contains(html, "iteration 07");
        Assert.IsFalse(html.Contains("server-version", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("Önskad frekvens", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("<svg", StringComparison.Ordinal));
    }
    [TestMethod]
    public async Task TestThat_receiver_view_renders_only_supplied_client_metadata_raw_filtered_and_graphs()
    {
        var store = new SensorDiagnosticsStore();
        var descriptor = new SensorDescriptor("test", "test-native", "Unit-test sensor", "Test fixture", SensorKind.Accelerometer, new(SensorReportingMode.Continuous), SensorStatus.Available);
        store.SetClient(new("test-client", ClientPlatform.Android, new() { ApplicationVersion = "client-version", GitCommitHash = "client-commit", LatestImplementedIteration = 5 }), [descriptor]);
        store.BeginSession(["test"], 50);
        foreach (var time in new long[] { 1000000000, 1200000000 })
            store.Receive(new("test", SensorKind.Accelerometer, [new("x", 1, "m/s²"), new("y", 2, "m/s²"), new("z", 3, "m/s²")], time, null,
                DateTimeOffset.UtcNow, SensorTimestampSource.AndroidElapsedRealtime, SensorQuality.High, "Unit-test fixture"));
        var html = await RenderAsync(store);
        StringAssert.Contains(html, "client-version"); StringAssert.Contains(html, "client-commit");
        StringAssert.Contains(html, "Active"); StringAssert.Contains(html, "High");
        StringAssert.Contains(html, "<svg"); StringAssert.Contains(html, "<polyline");
        StringAssert.Contains(html, "stroke-dasharray");
        Assert.IsFalse(html.Contains("server-commit", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("Önskad frekvens", StringComparison.Ordinal));
    }
}
