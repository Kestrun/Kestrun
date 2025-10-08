using System.Net;
using Kestrun.Hosting.Options;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Xunit;

namespace KestrunTests.Hosting.Options;

public class ListenerOptionsTests
{
    [Fact]
    [Trait("Category", "Hosting")]
    public void DefaultConstructor_InitializesDefaults()
    {
        var options = new ListenerOptions();

        Assert.Equal(IPAddress.Any, options.IPAddress);
        Assert.Equal(0, options.Port);
        Assert.False(options.UseHttps);
        Assert.Equal(HttpProtocols.Http1, options.Protocols);
        Assert.False(options.UseConnectionLogging);
        Assert.Null(options.X509Certificate);
        Assert.False(options.DisableAltSvcHeader);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void IPAddress_CanBeSet()
    {
        var options = new ListenerOptions { IPAddress = IPAddress.Loopback };

        Assert.Equal(IPAddress.Loopback, options.IPAddress);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Port_CanBeSet()
    {
        var options = new ListenerOptions { Port = 8080 };

        Assert.Equal(8080, options.Port);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void UseHttps_CanBeSet()
    {
        var options = new ListenerOptions { UseHttps = true };

        Assert.True(options.UseHttps);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Protocols_CanBeSet()
    {
        var options = new ListenerOptions { Protocols = HttpProtocols.Http2 };

        Assert.Equal(HttpProtocols.Http2, options.Protocols);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void UseConnectionLogging_CanBeSet()
    {
        var options = new ListenerOptions { UseConnectionLogging = true };

        Assert.True(options.UseConnectionLogging);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void DisableAltSvcHeader_CanBeSet()
    {
        var options = new ListenerOptions { DisableAltSvcHeader = true };

        Assert.True(options.DisableAltSvcHeader);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ToString_Http_ReturnsCorrectFormat()
    {
        var options = new ListenerOptions
        {
            IPAddress = IPAddress.Loopback,
            Port = 8080
        };

        Assert.Equal("http://127.0.0.1:8080", options.ToString());
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ToString_Https_ReturnsCorrectFormat()
    {
        var options = new ListenerOptions
        {
            IPAddress = IPAddress.Loopback,
            Port = 443,
            UseHttps = true
        };

        Assert.Equal("https://127.0.0.1:443", options.ToString());
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ToString_IPv6_ReturnsCorrectFormat()
    {
        var options = new ListenerOptions
        {
            IPAddress = IPAddress.IPv6Loopback,
            Port = 5000
        };

        Assert.Equal("http://::1:5000", options.ToString());
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ToString_AnyAddress_ReturnsCorrectFormat()
    {
        var options = new ListenerOptions { Port = 5000 };

        Assert.Equal("http://0.0.0.0:5000", options.ToString());
    }
}
