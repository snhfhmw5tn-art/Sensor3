using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
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
public sealed class PairingTransportTests
{
    [TestMethod]
    public async Task TestThat_redeemed_token_authenticates_real_https_signalr_and_replay_is_rejected()
    {
        using var certificates = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        certificates.Open(OpenFlags.ReadOnly);
        var certificate = certificates.Certificates.FirstOrDefault(x => x.HasPrivateKey && x.Subject == "CN=localhost" && x.NotAfter > DateTime.Now);
        if (certificate is null) Assert.Inconclusive("Befintligt localhost-certifikat krävs.");
        var directory = Path.Combine(Path.GetTempPath(), "Sensor3-pairing-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Pairing:StorageDirectory"] = directory });
            builder.WebHost.ConfigureKestrel(x => x.Listen(IPAddress.Loopback, 0, endpoint => endpoint.UseHttps(certificate!)));
            builder.AddSensorTelemetry();
            builder.Services.AddAntiforgery();
            builder.Services.AddSingleton(new BuildInfo { ApplicationVersion = "pairing-test" });
            await using var app = builder.Build();
            app.MapSensorTelemetry();
            app.MapDevicePairing();
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var http = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.Thumbprint == certificate!.Thumbprint });
            using var deniedQr = await http.GetAsync(address + "/devices");
            Assert.AreEqual(HttpStatusCode.Unauthorized, deniedQr.StatusCode);
            using var deniedCreate = await http.PostAsync(address + "/devices/create", new FormUrlEncodedContent([]));
            Assert.AreEqual(HttpStatusCode.Unauthorized, deniedCreate.StatusCode);
            var device = Guid.NewGuid().ToString("N");
            var invitation = app.Services.GetRequiredService<DevicePairingStore>().Create();
            using var response = await http.PostAsJsonAsync(address + "/api/pairing/redeem", new PairingRequest(invitation.Code, device));
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.Headers.CacheControl!.NoStore);
            var credential = await response.Content.ReadFromJsonAsync<PairingCredential>();
            Assert.IsNotNull(credential);
            using var replay = await http.PostAsJsonAsync(address + "/api/pairing/redeem", new PairingRequest(invitation.Code, Guid.NewGuid().ToString("N")));
            Assert.AreEqual(HttpStatusCode.BadRequest, replay.StatusCode);
            await using var connection = new HubConnectionBuilder().WithUrl(address + "/hubs/sensors", options =>
            {
                options.Headers["X-Sensor3-DeviceId"] = device;
                options.AccessTokenProvider = () => Task.FromResult<string?>(credential.Token);
                options.HttpMessageHandlerFactory = _ => new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert?.Thumbprint == certificate!.Thumbprint };
                options.WebSocketConfiguration = socket => socket.RemoteCertificateValidationCallback = (_, cert, _, _) => cert?.GetCertHashString() == certificate!.Thumbprint;
            }).Build();
            await connection.StartAsync();
            var registration = new TelemetryRegistration(new(device, ClientPlatform.Android, new()), Guid.NewGuid(), TelemetryMode.Research, [],
                Enumerable.Range(1, 18).Select(x => new IterationInfo(x, "test", "test", IterationStatus.NotStarted, "Unknown", null, null, "NotRun", [])).ToArray());
            Assert.AreEqual("pairing-test", (await connection.InvokeAsync<BuildInfo>("Register", registration)).ApplicationVersion);
            await connection.StopAsync();
            await app.StopAsync();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
