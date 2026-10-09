using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sensor3.Contracts;

namespace Sensor3.Distribution;

public static class DistributionHosting
{
    public const string AdminRole = "ReleaseAdministrator";
    public static void AddDistribution(this WebApplicationBuilder builder)
    {
        var options = new DistributionOptions();
        builder.Configuration.GetSection("Distribution").Bind(options);
        var storage = Path.GetFullPath(options.StorageRoot);
        var webroot = Path.GetFullPath(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
        if (storage.Equals(webroot, StringComparison.OrdinalIgnoreCase) || storage.StartsWith(webroot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Releasefiler måste lagras utanför webroot.");
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<ReleaseStore>();
        builder.Services.Configure<FormOptions>(x => { x.MultipartBodyLengthLimit = options.MaximumArtifactBytes + 65536; x.ValueLengthLimit = 32768; });
        builder.WebHost.ConfigureKestrel(x => x.Limits.MaxRequestBodySize = options.MaximumArtifactBytes + 65536);
        builder.Services.AddAntiforgery(x => x.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always);
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(x =>
        {
            x.Cookie.Name = "Sensor3.Distribution.Auth";
            x.Cookie.HttpOnly = true;
            x.Cookie.SameSite = SameSiteMode.Strict;
            x.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            x.LoginPath = "/admin/login";
            x.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            x.SlidingExpiration = false;
            x.Events.OnValidatePrincipal = context =>
            {
                if (context.Principal?.FindFirst("credential-stamp")?.Value != Stamp(options.AdminPasswordHash) || string.IsNullOrWhiteSpace(options.AdminUsername)) context.RejectPrincipal();
                return Task.CompletedTask;
            };
            x.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 401;
                else context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
            x.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(x =>
        {
            x.RejectionStatusCode = 429;
            x.AddPolicy("distribution-login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }
    public static void UseDistribution(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseAntiforgery();
    }
    public static void MapDistribution(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/releases", async (HttpContext context, ReleaseStore store, BuildInfo build) => await SafeAsync(context, async () =>
        {
            var releases = (await store.ListAsync(context.RequestAborted)).Where(x => !x.IsRevoked);
            if (context.Request.Query.TryGetValue("platform", out var platform)) { var selected = ParseEnum<ClientPlatform>(platform.ToString()); releases = releases.Where(x => x.Manifest.Platform == selected); }
            if (context.Request.Query.TryGetValue("channel", out var channel)) { var selected = ParseEnum<ReleaseChannel>(channel.ToString()); releases = releases.Where(x => x.Manifest.Channel == selected); }
            return Results.Json(releases.ToArray(), ReleaseStore.JsonOptions);
        }));
        endpoints.MapGet("/api/releases/latest", async (HttpContext context, ReleaseStore store, BuildInfo build) => await SafeAsync(context, async () =>
        {
            var latest = ReleaseSelection.Latest(await store.ListAsync(context.RequestAborted), ParseEnum<ClientPlatform>(context.Request.Query["platform"].ToString()),
                ParseEnum<ReleaseChannel>(context.Request.Query["channel"].ToString()), build.ApplicationVersion);
            return latest is null ? Results.NotFound() : Results.Json(latest, ReleaseStore.JsonOptions);
        }));
        endpoints.MapGet("/api/updates/check", async (HttpContext context, ReleaseStore store, BuildInfo build) => await SafeAsync(context, async () =>
        {
            if (!long.TryParse(context.Request.Query["buildNumber"], out var number)) throw new ArgumentException("Buildnummer saknas.");
            return Results.Json(ReleaseSelection.Check(await store.ListAsync(context.RequestAborted), ParseEnum<ClientPlatform>(context.Request.Query["platform"].ToString()),
                ParseEnum<ReleaseChannel>(context.Request.Query["channel"].ToString()), context.Request.Query["version"].ToString(), number, build.ApplicationVersion), ReleaseStore.JsonOptions);
        }));
        endpoints.MapGet("/api/releases/{id:guid}/download", async (Guid id, HttpContext context, ReleaseStore store) => await SafeAsync(context, async () =>
        {
            var download = await store.OpenDownloadAsync(id, context.RequestAborted);
            if (download is null) return Results.NotFound();
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.CacheControl = "no-store";
            return Results.File(download.Value.Content, download.Value.Release.Artifact.ContentType, download.Value.Release.Artifact.FileName, enableRangeProcessing: false);
        }));
        endpoints.MapGet("/api/admin/releases", async (HttpContext context, ReleaseStore store) => await SafeAsync(context, async () =>
            Results.Json(await store.ListAsync(context.RequestAborted), ReleaseStore.JsonOptions))).RequireAuthorization(p => p.RequireRole(AdminRole));

        endpoints.MapGet("/admin/login", (HttpContext context, IAntiforgery antiforgery, DistributionOptions options) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var configured = !string.IsNullOrWhiteSpace(options.AdminUsername) && !string.IsNullOrWhiteSpace(options.AdminPasswordHash);
            var token = antiforgery.GetAndStoreTokens(context);
            var body = configured ? $"""
                <form method="post" action="/admin/login">
                <input type="hidden" name="{WebUtility.HtmlEncode(token.FormFieldName)}" value="{WebUtility.HtmlEncode(token.RequestToken)}">
                <label>Användarnamn <input name="username" autocomplete="username" required></label>
                <label>Lösenord <input name="password" type="password" autocomplete="current-password" required></label>
                <button>Logga in</button></form>
                """ : "<p>Administration är avstängd tills administratörskontot har konfigurerats på servern.</p>";
            return Results.Content($"<!doctype html><html lang='sv'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Sensor 3 – administration</title><link rel='stylesheet' href='/distribution.css'><main><h1>Releaseadministration</h1>{body}<p><a href='/download'>Till nedladdningar</a></p></main></html>", "text/html");
        });
        endpoints.MapPost("/admin/login", async (HttpContext context, IAntiforgery antiforgery, DistributionOptions options) => await SafeAsync(context, async () =>
        {
            await antiforgery.ValidateRequestAsync(context);
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            if (string.IsNullOrWhiteSpace(options.AdminUsername) || string.IsNullOrWhiteSpace(options.AdminPasswordHash)) return Results.StatusCode(503);
            if (form["username"].ToString() != options.AdminUsername || !AdminCredentials.Verify(form["password"].ToString(), options.AdminPasswordHash)) return Results.Unauthorized();
            var identity = new ClaimsIdentity([new(ClaimTypes.Name, options.AdminUsername), new(ClaimTypes.Role, AdminRole), new("credential-stamp", Stamp(options.AdminPasswordHash))], CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false });
            return Results.Redirect("/admin/releases");
        })).RequireRateLimiting("distribution-login");
        endpoints.MapPost("/admin/logout", async (HttpContext context, IAntiforgery antiforgery) => await SafeAsync(context, async () =>
        {
            await antiforgery.ValidateRequestAsync(context);
            await context.SignOutAsync();
            return Results.Redirect("/download");
        })).RequireAuthorization(p => p.RequireRole(AdminRole));
        endpoints.MapPost("/admin/releases/publish", async (HttpContext context, IAntiforgery antiforgery, ReleaseStore store, BuildInfo build) => await SafeAsync(context, async () =>
        {
            await antiforgery.ValidateRequestAsync(context);
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var manifest = JsonSerializer.Deserialize<ReleaseManifest>(form["manifest"].ToString(), ReleaseStore.JsonOptions) ?? throw new ArgumentException("Release-manifest saknas.");
            if (manifest.IterationNumber > build.LatestImplementedIteration) throw new ArgumentException("Releasen får inte ange en ännu ej implementerad iteration.");
            if (form.Files.Count != 1) throw new ArgumentException("Välj exakt en installationsfil.");
            await using var content = form.Files[0].OpenReadStream();
            await store.PublishAsync(manifest, form.Files[0].FileName, content, context.RequestAborted);
            return Results.Redirect("/admin/releases");
        })).RequireAuthorization(p => p.RequireRole(AdminRole));
        endpoints.MapPost("/admin/releases/{id:guid}/revoke", async (Guid id, HttpContext context, IAntiforgery antiforgery, ReleaseStore store) => await SafeAsync(context, async () =>
        {
            await antiforgery.ValidateRequestAsync(context);
            return await store.RevokeAsync(id, context.RequestAborted) ? Results.Redirect("/admin/releases") : Results.NotFound();
        })).RequireAuthorization(p => p.RequireRole(AdminRole));
    }
    private static T ParseEnum<T>(string value) where T : struct, Enum => Enum.TryParse<T>(value, true, out var result) && Enum.IsDefined(result)
        ? result : throw new ArgumentException($"Ogiltigt eller saknat {typeof(T).Name}.");
    private static string Stamp(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static async Task<IResult> SafeAsync(HttpContext context, Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (AntiforgeryValidationException) { return Results.BadRequest(new { error = "Ogiltigt formulärskydd. Ladda om sidan." }); }
        catch (JsonException) { return Results.BadRequest(new { error = "Ogiltigt release-manifest." }); }
        catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            context.RequestServices.GetRequiredService<ILogger<ReleaseStore>>().LogError(exception, "Releasekatalog eller installationsfil otillgänglig");
            return Results.Problem("Releaseinformationen är tillfälligt otillgänglig.", statusCode: 503);
        }
    }
}
