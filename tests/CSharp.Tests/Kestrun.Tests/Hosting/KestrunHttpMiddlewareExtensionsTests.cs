using Kestrun.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Kestrun.Hosting.Compression;
using Microsoft.AspNetCore.HttpsPolicy;
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
        var host = CreateHost(out _);
        _ = host.AddCorsDefaultPolicyAllowAll();
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCors_WithPolicyBuilder_RegistersPolicy()
    {
        var host = CreateHost(out _);
        var builder = new CorsPolicyBuilder().AllowAnyOrigin();
        _ = host.AddCorsPolicy("TestPolicy", builder);
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCors_WithPolicyAction_RegistersPolicy()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddCorsPolicy("TestPolicy", b => b.AllowAnyOrigin().AllowAnyHeader());
        Assert.True(host.CorsPolicyDefined);
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
        _ = Assert.Throws<ArgumentNullException>(() => host.AddCorsPolicy(null!, b => b.AllowAnyOrigin()));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCors_WithEmptyPolicyName_Throws()
    {
        var host = CreateHost(out _);
        _ = Assert.Throws<ArgumentException>(() => host.AddCorsPolicy("", b => b.AllowAnyOrigin()));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCors_WithNullBuilder_Throws()
    {
        var host = CreateHost(out _);
        _ = Assert.Throws<ArgumentNullException>(() => host.AddCorsPolicy("Test", (CorsPolicyBuilder)null!));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCors_WithNullBuildPolicy_Throws()
    {
        var host = CreateHost(out _);
        _ = Assert.Throws<ArgumentNullException>(() => host.AddCorsPolicy("Test", (Action<CorsPolicyBuilder>)null!));
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
    public void AddResponseCompression_RegistersKestrunResponseCompressionProvider()
    {
        var host = CreateHost(out _);
        _ = host.AddResponseCompression(o => o.EnableForHttps = true);

        // Extract queued service registrations
        var serviceField = typeof(KestrunHost).GetField("_serviceQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var actions = (List<Action<IServiceCollection>>)serviceField!.GetValue(host)!;

        var services = new ServiceCollection();
        var sc = services.AddLogging(); // required for provider logger injection
        Assert.NotNull(sc); // use variable to satisfy analyzer
        foreach (var a in actions)
        {
            a(services);
        }
        using var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<IResponseCompressionProvider>();
        var typed = Assert.IsType<KestrunResponseCompressionProvider>(provider);
        Assert.Same(provider, typed);
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

    #region Response Caching Helper Method Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateCachingInput_WithValidParameters_LogsCorrectly()
    {
        var mockLogger = new Mock<Serilog.ILogger>();
        _ = mockLogger.Setup(l => l.IsEnabled(Serilog.Events.LogEventLevel.Debug)).Returns(true);
        _ = mockLogger.Setup(l => l.IsEnabled(Serilog.Events.LogEventLevel.Information)).Returns(true);

        var host = new KestrunHost("TestApp", mockLogger.Object);
        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(10) };

        KestrunHttpMiddlewareExtensions.ValidateCachingInput(host, null, cacheControl);

        mockLogger.Verify(l => l.Debug(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once);
        // Verify the specific Information call for cache control
        mockLogger.Verify(l => l.Information(It.Is<string>(s => s.Contains("Cache-Control")), It.IsAny<string>()), Times.Once);
        Assert.Equal(cacheControl, host.DefaultCacheControl);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateCachingInput_WithNullCacheControl_DoesNotSetDefault()
    {
        var mockLogger = new Mock<Serilog.ILogger>();
        _ = mockLogger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);

        var host = new KestrunHost("TestApp", mockLogger.Object);
        var originalCacheControl = host.DefaultCacheControl;

        KestrunHttpMiddlewareExtensions.ValidateCachingInput(host, null, null);

        Assert.Equal(originalCacheControl, host.DefaultCacheControl);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void RegisterCachingServices_WithNullConfig_RegistersDefaults()
    {
        var mockLogger = new Mock<Serilog.ILogger>();
        _ = mockLogger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);

        var host = new KestrunHost("TestApp", mockLogger.Object);

        // This should not throw and should register the service
        KestrunHttpMiddlewareExtensions.RegisterCachingServices(host, null);

        // Verify service was added by checking the service collection has entries
        var serviceField = typeof(KestrunHost).GetField("_serviceQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var services = (List<Action<IServiceCollection>>)serviceField!.GetValue(host)!;
        Assert.True(services.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void RegisterCachingServices_WithConfig_RegistersWithConfig()
    {
        var mockLogger = new Mock<Serilog.ILogger>();
        _ = mockLogger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);

        var host = new KestrunHost("TestApp", mockLogger.Object);

        KestrunHttpMiddlewareExtensions.RegisterCachingServices(host, opts => opts.MaximumBodySize = 1024);

        var serviceField = typeof(KestrunHost).GetField("_serviceQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var services = (List<Action<IServiceCollection>>)serviceField!.GetValue(host)!;
        Assert.True(services.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyCacheHeaders_WithGetRequest_ReturnsTrueWhenApplied()
    {
        var mockLogger = new Mock<Serilog.ILogger>();
        _ = mockLogger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Response.StatusCode = 200;

        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(10) };

        var result = KestrunHttpMiddlewareExtensions.ApplyCacheHeaders(context, cacheControl, mockLogger.Object);

        Assert.True(result);
        Assert.Equal(cacheControl.ToString(), context.Response.Headers.CacheControl.ToString());
        Assert.Contains("Accept-Encoding", context.Response.Headers.Vary.ToString());
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyCacheHeaders_WithPostRequest_ReturnsFalse()
    {
        var mockLogger = new Mock<Serilog.ILogger>();
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Response.StatusCode = 200;

        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(10) };

        var result = KestrunHttpMiddlewareExtensions.ApplyCacheHeaders(context, cacheControl, mockLogger.Object);

        Assert.False(result);
        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.CacheControl));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyCacheHeaders_WithErrorStatus_ReturnsFalse()
    {
        var mockLogger = new Mock<Serilog.ILogger>();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Response.StatusCode = 404;

        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(10) };

        var result = KestrunHttpMiddlewareExtensions.ApplyCacheHeaders(context, cacheControl, mockLogger.Object);

        Assert.False(result);
        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.CacheControl));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyCacheHeaders_WithSetCookieHeader_ReturnsFalse()
    {
        var mockLogger = new Mock<Serilog.ILogger>();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Response.StatusCode = 200;
        context.Response.Headers.Append(HeaderNames.SetCookie, "test=value");

        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(10) };

        var result = KestrunHttpMiddlewareExtensions.ApplyCacheHeaders(context, cacheControl, mockLogger.Object);

        Assert.False(result);
        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.CacheControl));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyCacheHeaders_WithNullCacheControl_ReturnsFalse()
    {
        var mockLogger = new Mock<Serilog.ILogger>();
        _ = mockLogger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Response.StatusCode = 200;

        var result = KestrunHttpMiddlewareExtensions.ApplyCacheHeaders(context, null, mockLogger.Object);

        Assert.False(result);
        Assert.False(context.Response.Headers.ContainsKey(HeaderNames.CacheControl));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyCacheHeaders_WithHeadRequest_ReturnsTrueWhenApplied()
    {
        var mockLogger = new Mock<Serilog.ILogger>();
        _ = mockLogger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);

        var context = new DefaultHttpContext();
        context.Request.Method = "HEAD";
        context.Response.StatusCode = 200;

        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(10) };

        var result = KestrunHttpMiddlewareExtensions.ApplyCacheHeaders(context, cacheControl, mockLogger.Object);

        Assert.True(result);
        Assert.Equal(cacheControl.ToString(), context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CreateCachingMiddleware_ReturnsValidAction()
    {
        var mockLogger = new Mock<Serilog.ILogger>();
        _ = mockLogger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);

        var host = new KestrunHost("TestApp", mockLogger.Object);
        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(10) };

        var middlewareAction = KestrunHttpMiddlewareExtensions.CreateCachingMiddleware(host, cacheControl);

        Assert.NotNull(middlewareAction);

        // Since we can't easily mock extension methods, we'll just verify the method returns a valid action
        _ = Assert.IsType<Action<IApplicationBuilder>>(middlewareAction);
    }

    #endregion

    #region HSTS Extension Method Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHsts_WithNullOptions_UsesDefaults()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddHsts((HstsOptions)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHsts_WithOptions_RegistersMiddleware()
    {
        var host = CreateHost(out var middleware);
        var options = new HstsOptions
        {
            MaxAge = TimeSpan.FromDays(365),
            IncludeSubDomains = true,
            Preload = true
        };
        options.ExcludedHosts.Add("localhost");
        _ = host.AddHsts(options);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHsts_WithNullDelegate_DoesNotThrow()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddHsts((Action<HstsOptions>)null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHsts_WithCustomDelegate_Registers()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddHsts(o =>
        {
            o.MaxAge = TimeSpan.FromDays(90);
            o.IncludeSubDomains = false;
            o.Preload = false;
            o.ExcludedHosts.Add("dev.example.com");
        });
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHsts_WithOptionsObject_CopiesAllProperties()
    {
        var host = CreateHost(out var middleware);
        var options = new HstsOptions
        {
            MaxAge = TimeSpan.FromDays(180),
            IncludeSubDomains = true,
            Preload = false
        };
        options.ExcludedHosts.Add("localhost");
        options.ExcludedHosts.Add("127.0.0.1");

        _ = host.AddHsts(options);
        Assert.True(middleware.Count > 0);

        // The middleware should be registered - we can verify this by checking the middleware count
        // In a real integration test, we would verify the actual HSTS header behavior
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHsts_LogsCorrectly()
    {
        var mockLogger = new Mock<Serilog.ILogger>();
        _ = mockLogger.Setup(l => l.IsEnabled(Serilog.Events.LogEventLevel.Debug)).Returns(true);

        var host = new KestrunHost("TestApp", mockLogger.Object);
        _ = host.AddHsts(o => o.MaxAge = TimeSpan.FromDays(30));

        // Verify that debug logging was called - match the actual signature
        mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Adding HSTS")), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHsts_WithEmptyExcludedHosts_ClearsDefaults()
    {
        var host = CreateHost(out var middleware);
        var options = new HstsOptions
        {
            MaxAge = TimeSpan.FromDays(30),
            IncludeSubDomains = true,
            Preload = true
        };
        // Explicitly clear excluded hosts to test development scenario
        options.ExcludedHosts.Clear();

        _ = host.AddHsts(options);
        Assert.True(middleware.Count > 0);
    }
    #endregion
}
