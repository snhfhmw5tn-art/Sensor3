using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using QRCoder;

namespace Sensor3.Infrastructure;
public static class DevicePairingHosting
{
    public static void MapDevicePairing(this WebApplication app)
    {
        app.MapGet("/devices", (HttpContext context, IAntiforgery antiforgery) => Page(context, antiforgery, null, app.Configuration))
            .RequireAuthorization(p => p.RequireRole("ReleaseAdministrator"));
        app.MapPost("/devices/create", async (HttpContext context, IAntiforgery antiforgery, DevicePairingStore store) =>
        {
            if (!context.Request.IsHttps) return Results.BadRequest("HTTPS krävs.");
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.BadRequest("Ladda om sidan och försök igen."); }
            return Page(context, antiforgery, store.Create(), app.Configuration);
        }).RequireAuthorization(p => p.RequireRole("ReleaseAdministrator"));
        app.MapGet("/pair", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            return Results.Content("""
                <!doctype html><html lang="sv"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
                <title>Registrera Sensor3</title><link rel="stylesheet" href="/distribution.css"><main><h1>Registrera telefonen</h1>
                <p>Öppna Sensor3 för att registrera enheten. Du bekräftar serveradressen i appen innan en token hämtas.</p>
                <a id="open">Öppna Sensor3</a><p>Om appen inte öppnas: installera den senaste Android-klienten eller klistra in QR-länken i klientens registreringsfält.</p></main>
                <script>
                const code = location.hash.slice(1);
                if (/^[A-F0-9]{64}$/.test(code) && location.protocol === 'https:') {
                  document.getElementById('open').href = 'sensor3://pair?server=' + encodeURIComponent(location.origin) + '&code=' + code;
                } else { document.getElementById('open').textContent = 'Ogiltig registreringslänk'; }
                </script></html>
                """, "text/html");
        });
    }
    private static IResult Page(HttpContext context, IAntiforgery antiforgery, PairingInvitation? invitation, IConfiguration configuration)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        var server = new Uri(configuration["Pairing:PublicServer"] ?? "https://prototyp.qsys.se");
        if (server.Scheme != "https" || server.AbsolutePath != "/" || server.Query.Length != 0 || server.Fragment.Length != 0 || server.UserInfo.Length != 0)
            throw new InvalidOperationException("Pairing:PublicServer måste vara en HTTPS-serveradress.");
        var csrf = antiforgery.GetAndStoreTokens(context);
        var qr = "";
        if (invitation is not null)
        {
            var link = new Uri(server, "/pair").AbsoluteUri + "#" + invitation.Code;
            using var data = QRCodeGenerator.GenerateQrCode(link, QRCodeGenerator.ECCLevel.Q);
            using var renderer = new PngByteQRCode(data);
            qr = $"<img width='300' height='300' alt='QR-kod för engångsregistrering' src='data:image/png;base64,{Convert.ToBase64String(renderer.GetGraphic(8))}'><p>Giltig till {invitation.ExpiresAt:HH:mm} UTC. Kan användas en gång.</p><label>Registreringslänk <input readonly value='{WebUtility.HtmlEncode(link)}'></label>";
        }
        return Results.Content($"""
            <!doctype html><html lang="sv"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Sensor3 · Registrera enhet</title><link rel="stylesheet" href="/distribution.css"><main>
            <a href="/">← Dashboard</a><h1>Registrera enhet</h1><p>Skapa en QR-kod och skanna den med telefonens kamera. Öppna länken och välj Öppna Sensor3.</p>
            <p>Server: {WebUtility.HtmlEncode(server.AbsoluteUri)}. Koden gäller i fem minuter. Dela den bara med den enhet som ska registreras.</p>
            {qr}<form method="post" action="/devices/create"><input type="hidden" name="{WebUtility.HtmlEncode(csrf.FormFieldName)}" value="{WebUtility.HtmlEncode(csrf.RequestToken)}"><button>Skapa QR-kod</button></form></main></html>
            """, "text/html");
    }
}
