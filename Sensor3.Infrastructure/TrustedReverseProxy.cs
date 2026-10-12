using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sensor3.Infrastructure;

public static class TrustedReverseProxy
{
    public static void AddTrustedReverseProxy(this WebApplicationBuilder builder)
    {
        var configured = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        builder.Services.Configure<ForwardedHeadersOptions>(options => Configure(options, configured));
    }
    public static void Configure(ForwardedHeadersOptions options, IReadOnlyList<string> configured)
    {
        var addresses = configured.Select(value => IPAddress.TryParse(value, out var address)
            && !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any)
            ? address : throw new InvalidOperationException("ReverseProxy:KnownProxies måste innehålla exakta proxy-IP-adresser.")).ToArray();
        options.ForwardedHeaders = addresses.Length == 0 ? ForwardedHeaders.None
            : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var address in addresses) options.KnownProxies.Add(address);
    }
}
