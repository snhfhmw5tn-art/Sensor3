using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sensor3.Infrastructure;

namespace Sensor3.Tests;
[TestClass]
public sealed class TrustedReverseProxyTests
{
    private static async Task<DefaultHttpContext> Forward(string remote, params string[] proxies)
    {
        var options = new ForwardedHeadersOptions(); TrustedReverseProxy.Configure(options, proxies);
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
        var context = new DefaultHttpContext(); context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        context.Request.Scheme = "http"; context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.27";
        await middleware.Invoke(context); return context;
    }
    [TestMethod]
    public async Task TestThat_only_the_configured_proxy_can_forward_https()
    {
        var trusted = await Forward("10.0.23.17", "10.0.23.17");
        Assert.IsTrue(trusted.Request.IsHttps); Assert.AreEqual(IPAddress.Parse("203.0.113.27"), trusted.Connection.RemoteIpAddress);
        var unknown = await Forward("10.0.23.99", "10.0.23.17");
        Assert.IsFalse(unknown.Request.IsHttps); Assert.AreEqual(IPAddress.Parse("10.0.23.99"), unknown.Connection.RemoteIpAddress);
    }
    [TestMethod]
    public async Task TestThat_empty_configuration_does_not_trust_forwarded_headers_even_from_loopback()
    {
        Assert.IsFalse((await Forward("127.0.0.1")).Request.IsHttps);
    }
    [TestMethod]
    [DataRow("0.0.0.0", DisplayName = "IPv4 wildcard")]
    [DataRow("::", DisplayName = "IPv6 wildcard")]
    [DataRow("10.0.23.0/24", DisplayName = "Subnet rejected")]
    [DataRow("proxy.example", DisplayName = "Hostname rejected")]
    public void TestThat_proxy_configuration_requires_exact_ip_addresses(string address)
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => TrustedReverseProxy.Configure(new(), [address]));
    }
}
