using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sensor3.Contracts;
using Sensor3.Infrastructure;

namespace Sensor3.Tests;

[TestClass]
public sealed class TelemetryTransportTests
{
    [TestMethod]
    public async Task TestThat_real_https_signalr_requires_token_and_deduplicates_after_reconnect()
    {
        using var certificates = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        certificates.Open(OpenFlags.ReadOnly);
        var certificate = certificates.Certificates.FirstOrDefault(x => x.HasPrivateKey && x.Subject == "CN=localhost" && x.NotAfter > DateTime.Now);
        if (certificate is null) Assert.Inconclusive("Befintligt localhost-certifikat krävs; testet skapar inte signeringsnycklar.");
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(x => x.Listen(IPAddress.Loopback, 0, endpoint => endpoint.UseHttps(certificate!)));
        const string token = "test-only-credential-for-local-loopback";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Telemetry:DeviceTokenHashes:test-device"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) });
        builder.AddSensorTelemetry(); builder.Services.AddAuthentication(DeviceAuthentication.SchemeName);
        builder.Services.AddSingleton(new BuildInfo { ApplicationVersion = "test-server" });
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapSensorTelemetry();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        HubConnection Connection(string credential) => new HubConnectionBuilder().WithUrl(address + "/hubs/sensors", options =>
        {
            options.Headers["X-Sensor3-DeviceId"] = "test-device"; options.AccessTokenProvider = () => Task.FromResult<string?>(credential);
            options.HttpMessageHandlerFactory = _ => new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.Thumbprint == certificate!.Thumbprint };
            options.WebSocketConfiguration = socket => socket.RemoteCertificateValidationCallback = (_, cert, _, _) => cert?.GetCertHashString() == certificate!.Thumbprint;
        }).Build();
        await using var denied = Connection("wrong-token");
        await Assert.ThrowsExactlyAsync<HttpRequestException>(async () => await denied.StartAsync());
        var registration = new TelemetryRegistration(new("test-device", ClientPlatform.Android, new()), Guid.NewGuid(), TelemetryMode.Research,
            [new("accel", "native", "actual descriptor", "vendor", SensorKind.Accelerometer, new(SensorReportingMode.Continuous), SensorStatus.Available)],
            Enumerable.Range(1, 18).Select(x => new IterationInfo(x, "test", "test", IterationStatus.NotStarted, "Unknown", null, null, "NotRun", [])).ToArray());
        var batch = new TelemetryBatch(registration.SessionId, 0, DateTimeOffset.UtcNow, [new(new("accel", SensorKind.Accelerometer, [new("x", 1, "m/s²")], 123456789L, null, DateTimeOffset.UtcNow, SensorTimestampSource.AndroidElapsedRealtime, SensorQuality.Medium, "device"))]);
        await using var connection = Connection(token); await connection.StartAsync();
        Assert.AreEqual("test-server", (await connection.InvokeAsync<BuildInfo>("Register", registration)).ApplicationVersion);
        Assert.IsFalse((await connection.InvokeAsync<TelemetryAcknowledgement>("Send", batch)).Duplicate);
        await connection.StopAsync(); await connection.StartAsync(); await connection.InvokeAsync<BuildInfo>("Register", registration);
        Assert.IsTrue((await connection.InvokeAsync<TelemetryAcknowledgement>("Send", batch)).Duplicate);
        Assert.AreEqual(1L, app.Services.GetRequiredService<TelemetryRegistry>().GetDiagnostics(registration.SessionId).Sensors.Single().Count);
        builder.Configuration["Telemetry:DeviceTokenHashes:test-device"] = null; ((IConfigurationRoot)builder.Configuration).Reload();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await connection.InvokeAsync<TelemetryAcknowledgement>("Send", batch with { Sequence = 1 }));
        Assert.AreEqual(1L, app.Services.GetRequiredService<TelemetryRegistry>().GetSessions().Single().Statistics.Packets);
        await app.StopAsync();
    }
}
