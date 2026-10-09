using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Sensor3;
using Sensor3.Contracts;
using Sensor3.Sensors;

// Same Windows backend as the native app; no test/simulated sensor is injected.
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
var backend = new WindowsSensorBackend(NullLogger<WindowsSensorBackend>.Instance);
await using var provider = new SensorProvider(backend, new(), NullLogger<SensorProvider>.Instance);
var catalogue = await provider.DiscoverAsync(timeout.Token);
var measured = false;
if (args.Contains("--measure-imu"))
{
    var selected = catalogue.Where(x => x.Status == SensorStatus.Available && x.Kind is SensorKind.Accelerometer or SensorKind.Gyroscope).Select(x => x.Id).ToArray();
    if (selected.Length > 0)
    {
        await provider.StartAsync(selected, new() { FrequencyHz = 50 }, timeout.Token);
        await Task.Delay(TimeSpan.FromSeconds(5), timeout.Token);
        await provider.StopAsync(timeout.Token);
        measured = true;
    }
}
var result = new { capturedAtUtc = DateTimeOffset.UtcNow, catalogue, imuMeasurementAttempted = measured, statistics = provider.GetStatistics() };
var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } });
var output = args.FirstOrDefault(x => x.StartsWith("--output=", StringComparison.Ordinal));
if (output is not null) await File.WriteAllTextAsync(output[9..], json, timeout.Token);
Console.WriteLine($"Windows native discovery: {catalogue.Count(x => x.Status == SensorStatus.Available)} available, {catalogue.Count(x => x.Status == SensorStatus.Unsupported)} unsupported, {catalogue.Count(x => x.Status == SensorStatus.PermissionRequired)} permission required, {catalogue.Count(x => x.Status == SensorStatus.Error)} errors; IMU measurement attempted={measured}.");
foreach (var sample in provider.GetStatistics()) Console.WriteLine($"{sample.SensorId}: count={sample.ReceivedCount}; frequency={sample.ActualFrequencyHz?.ToString("F2") ?? "Unknown"} Hz; rejected={sample.RejectedCount}");
return catalogue.Any(x => x.Status == SensorStatus.Error) ? 1 : 0;
