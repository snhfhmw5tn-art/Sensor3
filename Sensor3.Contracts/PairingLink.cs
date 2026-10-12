namespace Sensor3.Contracts;
public sealed record PairingLink(Uri Server, string Code)
{
    public static PairingLink Parse(string value)
    {
        if (value.Length > 2048 || !Uri.TryCreate(value, UriKind.Absolute, out var link)) throw new ArgumentException("Ogiltig QR-länk.");
        Uri server;
        string code;
        if (link.Scheme == "sensor3" && link.Host == "pair")
        {
            var query = link.Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => x.Length == 2 ? Uri.UnescapeDataString(x[1]) : "");
            if (!query.TryGetValue("server", out var address) || !Uri.TryCreate(address, UriKind.Absolute, out server!) || !query.TryGetValue("code", out code!)) throw new ArgumentException("Ogiltig QR-länk.");
        }
        else if (link.Scheme == "https" && link.AbsolutePath == "/pair" && link.Query.Length == 0)
        {
            server = new Uri(link.GetLeftPart(UriPartial.Authority));
            code = link.Fragment.TrimStart('#');
        }
        else throw new ArgumentException("Ogiltig QR-länk.");
        if (server.Scheme != "https" || server.UserInfo.Length != 0 || server.AbsolutePath != "/" || server.Query.Length != 0 || server.Fragment.Length != 0
            || code.Length != 64 || code.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("QR-länken måste använda HTTPS och en giltig registreringskod.");
        return new(server, code);
    }
}
