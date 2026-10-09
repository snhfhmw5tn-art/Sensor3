using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Sensor3.Contracts;

namespace Sensor3.Infrastructure;

public sealed class TelemetryAuthOptions { public Dictionary<string, string> DeviceTokenHashes { get; set; } = []; }
public sealed class DeviceAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IOptionsMonitor<TelemetryAuthOptions> credentials) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TelemetryDevice";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.IsHttps) return Task.FromResult(AuthenticateResult.Fail("HTTPS krävs."));
        var device = Request.Headers["X-Sensor3-DeviceId"].ToString();
        var authorization = Request.Headers.Authorization.ToString();
        if (device.Length is < 1 or > 64 || !authorization.StartsWith("Bearer ", StringComparison.Ordinal) || authorization.Length > 512 ||
            !credentials.CurrentValue.DeviceTokenHashes.TryGetValue(device, out var expected)) return Task.FromResult(AuthenticateResult.Fail("Enhetsbehörighet saknas."));
        try
        {
            var actual = SHA256.HashData(Encoding.UTF8.GetBytes(authorization[7..]));
            if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expected))) return Task.FromResult(AuthenticateResult.Fail("Enhetsbehörighet nekad."));
            var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, device), new("credential", expected)], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), SchemeName)));
        }
        catch (FormatException) { return Task.FromResult(AuthenticateResult.Fail("Enhetsbehörighet felkonfigurerad.")); }
    }
}
public sealed class TelemetryQueue
{
    public sealed record Work(string Device, TelemetryBatch Batch, TaskCompletionSource<bool> Completion);
    internal Channel<Work> Channel { get; } = System.Threading.Channels.Channel.CreateBounded<Work>(new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    public async Task<bool> SubmitAsync(string device, TelemetryBatch batch, CancellationToken token)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await Channel.Writer.WriteAsync(new(device, batch, completion), token);
        return await completion.Task.WaitAsync(token);
    }
}
public sealed class TelemetryProcessor(TelemetryQueue queue, TelemetryRegistry registry, ILogger<TelemetryProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
        await foreach (var work in queue.Channel.Reader.ReadAllAsync(stoppingToken))
            try { work.Completion.TrySetResult(registry.Apply(work.Device, work.Batch)); }
            catch (Exception exception) { logger.LogWarning("Sensorpaket avvisades: {Type}", exception.GetType().Name); work.Completion.TrySetException(exception); }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { while (queue.Channel.Reader.TryRead(out var work)) work.Completion.TrySetCanceled(); }
    }
}
[Authorize(AuthenticationSchemes = DeviceAuthentication.SchemeName)]
public sealed class TelemetryHub(TelemetryRegistry registry, TelemetryQueue queue, BuildInfo build, IOptionsMonitor<TelemetryAuthOptions> credentials) : Hub
{
    private string Device => Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private void ValidateCredential()
    {
        if (!credentials.CurrentValue.DeviceTokenHashes.TryGetValue(Device, out var hash) || hash != Context.User!.FindFirstValue("credential"))
        { Context.Abort(); throw new HubException("Enhetsbehörigheten har återkallats."); }
    }
    public BuildInfo Register(TelemetryRegistration registration)
    {
        ValidateCredential(); registry.Register(Device, registration, Context.ConnectionId); Context.Items["session"] = registration.SessionId; return build;
    }
    public async Task<TelemetryAcknowledgement> Send(TelemetryBatch batch)
    {
        ValidateCredential();
        if (Context.Items["session"] is not Guid session || session != batch.SessionId) throw new HubException("Registrera sessionen före överföring.");
        var duplicate = await queue.SubmitAsync(Device, batch, Context.ConnectionAborted);
        return new(batch.Sequence, duplicate, build);
    }
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items["session"] is Guid session) registry.Disconnect(Device, session, Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
public static class TelemetryHosting
{
    public static void AddSensorTelemetry(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<TelemetryAuthOptions>(builder.Configuration.GetSection("Telemetry"));
        builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, DeviceAuthentication>(DeviceAuthentication.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSignalR(x => { x.MaximumReceiveMessageSize = 1024 * 1024; x.MaximumParallelInvocationsPerClient = 1; });
        builder.Services.AddSingleton<TelemetryRegistry>();
        builder.Services.AddSingleton<IRealtimeAnalysisSource>(x => x.GetRequiredService<TelemetryRegistry>());
        builder.Services.AddSingleton<IRealtimeDiagnosticsSource>(x => x.GetRequiredService<TelemetryRegistry>());
        builder.Services.AddSingleton<TelemetryQueue>(); builder.Services.AddHostedService<TelemetryProcessor>();
    }
    public static void MapSensorTelemetry(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.IsHttps && (context.Request.Path.StartsWithSegments("/hubs/sensors") || context.Request.Path.StartsWithSegments("/api/telemetry")))
            { context.Response.StatusCode = StatusCodes.Status400BadRequest; await context.Response.WriteAsync("HTTPS krävs."); return; }
            await next(context);
        });
        app.MapHub<TelemetryHub>("/hubs/sensors");
        app.MapGet("/api/telemetry/sessions", (TelemetryRegistry registry) => registry.GetSessions()).RequireAuthorization(x => x.RequireRole("ReleaseAdministrator"));
        app.MapGet("/api/telemetry/sessions/{id:guid}/analysis", (Guid id, TelemetryRegistry registry) => registry.GetAnalysis(id)).RequireAuthorization(x => x.RequireRole("ReleaseAdministrator"));
        app.MapGet("/api/telemetry/sessions/{id:guid}", (Guid id, TelemetryRegistry registry) => registry.GetDiagnostics(id)).RequireAuthorization(x => x.RequireRole("ReleaseAdministrator"));
    }
}
