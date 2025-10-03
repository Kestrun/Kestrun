using Kestrun.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpsPolicy;
using Moq;
using Xunit;

namespace KestrunTests.Hosting;

public class KestrunHttpsRedirectionTests
{
    private KestrunHost CreateHost(out List<Action<IApplicationBuilder>> middleware)
    {
        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);
        var host = new KestrunHost("TestHttpsRedirect", logger.Object);
        var field = typeof(KestrunHost).GetField("_middlewareQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        middleware = (List<Action<IApplicationBuilder>>)field!.GetValue(host)!;
        return host;
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHttpsRedirection_WithNullOptions_UsesDefaults()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddHttpsRedirection((HttpsRedirectionOptions)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHttpsRedirection_WithOptions_RegistersMiddleware()
    {
        var host = CreateHost(out var middleware);
        var options = new HttpsRedirectionOptions { RedirectStatusCode = 308, HttpsPort = 5443 };
        _ = host.AddHttpsRedirection(options);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHttpsRedirection_WithNullDelegate_DoesNotThrow()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddHttpsRedirection((Action<HttpsRedirectionOptions>)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHttpsRedirection_WithCustomDelegate_Registers()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddHttpsRedirection(o => { o.RedirectStatusCode = 301; o.HttpsPort = 6001; });
        Assert.True(middleware.Count > 0);
    }
}
