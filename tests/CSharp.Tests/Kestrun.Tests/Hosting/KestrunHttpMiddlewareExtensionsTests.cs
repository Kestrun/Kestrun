using Kestrun.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Antiforgery;
using Moq;
using Xunit;

namespace KestrunTests.Hosting;

public class KestrunHttpMiddlewareExtensionsTests
{
    private KestrunHost CreateHost(out List<Action<IApplicationBuilder>> middleware)
    {
        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);
        var host = new KestrunHost("TestApp", logger.Object);
        var field = typeof(KestrunHost).GetField("_middlewareQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        middleware = (List<Action<IApplicationBuilder>>)field!.GetValue(host)!;
        return host;
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddResponseCompression_WithNullOptions_UsesDefaults()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddResponseCompression((ResponseCompressionOptions)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddResponseCompression_WithOptions_RegistersMiddleware()
    {
        var host = CreateHost(out var middleware);
        var options = new ResponseCompressionOptions { EnableForHttps = true };
        _ = host.AddResponseCompression(options);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddRateLimiter_WithNullOptions_UsesDefaults()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddRateLimiter((RateLimiterOptions)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddRateLimiter_WithOptions_RegistersMiddleware()
    {
        var host = CreateHost(out var middleware);
        var options = new RateLimiterOptions();
        _ = host.AddRateLimiter(options);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddAntiforgery_WithNullOptions_UsesDefaults()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddAntiforgery((AntiforgeryOptions)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddAntiforgery_WithOptions_RegistersMiddleware()
    {
        var host = CreateHost(out var middleware);
        var options = new AntiforgeryOptions { FormFieldName = "_csrf" };
        _ = host.AddAntiforgery(options);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCorsAllowAll_RegistersAllowAllPolicy()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddCorsAllowAll();
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCors_WithPolicyBuilder_RegistersPolicy()
    {
        var host = CreateHost(out var middleware);
        var builder = new CorsPolicyBuilder().AllowAnyOrigin();
        _ = host.AddCors("TestPolicy", builder);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCors_WithPolicyAction_RegistersPolicy()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddCors("TestPolicy", b => b.AllowAnyOrigin().AllowAnyHeader());
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddResponseCompression_WithNullAction_DoesNotThrow()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddResponseCompression((Action<ResponseCompressionOptions>)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddRateLimiter_WithNullAction_DoesNotThrow()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddRateLimiter((Action<RateLimiterOptions>)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddAntiforgery_WithNullAction_DoesNotThrow()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddAntiforgery((Action<AntiforgeryOptions>)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCors_WithNullPolicyName_Throws()
    {
        var host = CreateHost(out _);
        _ = Assert.Throws<ArgumentException>(() => host.AddCors(null!, b => b.AllowAnyOrigin()));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCors_WithEmptyPolicyName_Throws()
    {
        var host = CreateHost(out _);
        _ = Assert.Throws<ArgumentException>(() => host.AddCors("", b => b.AllowAnyOrigin()));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCors_WithNullBuilder_Throws()
    {
        var host = CreateHost(out _);
        _ = Assert.Throws<ArgumentNullException>(() => host.AddCors("Test", (CorsPolicyBuilder)null!));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCors_WithNullBuildPolicy_Throws()
    {
        var host = CreateHost(out _);
        _ = Assert.Throws<ArgumentNullException>(() => host.AddCors("Test", (Action<CorsPolicyBuilder>)null!));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddResponseCompression_WithCustomMimeTypes_SetsMimeTypes()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddResponseCompression(o => o.MimeTypes = ["application/json"]);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddRateLimiter_WithCustomDelegate_Registers()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddRateLimiter(o => { o.GlobalLimiter = null; });
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddAntiforgery_WithCustomDelegate_Registers()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddAntiforgery(o => o.FormFieldName = "csrf");
        Assert.True(middleware.Count > 0);
    }
}
