using Kestrun.Hosting;
using Kestrun.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.HttpsPolicy;
using Moq;
using Serilog.Events;
using Xunit;

namespace KestrunTests.Hosting;

/// <summary>
/// Tests for KestrunHttpMiddlewareExtensions internal helper methods and method overloads.
/// This supplements the main test file with additional test coverage for lesser-tested methods.
/// </summary>
public class KestrunHttpMiddlewareExtensionsAdditionalTests
{
    private KestrunHost CreateHost(out List<Action<IApplicationBuilder>> middleware)
    {
        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(false);
        var host = new KestrunHost("TestApp", logger.Object);
        var field = typeof(KestrunHost).GetField("_middlewareQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        middleware = (List<Action<IApplicationBuilder>>)field!.GetValue(host)!;
        return host;
    }

    #region AddCommonAccessLog Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCommonAccessLog_WithActionDelegate_RegistersMiddleware()
    {
        // Arrange
        var host = CreateHost(out var middleware);

        // Act
        var result = host.AddCommonAccessLog(opts =>
        {
            opts.IncludeQueryString = true;
            opts.IncludeProtocol = true;
        });

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCommonAccessLog_WithNullDelegate_UsesDefaults()
    {
        // Arrange
        var host = CreateHost(out var middleware);

        // Act - Explicitly cast to Action delegate to avoid ambiguity
        var result = host.AddCommonAccessLog((Action<CommonAccessLogOptions>?)null);

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddCommonAccessLog_WithMultipleConfigurations_BothRegistered()
    {
        // Arrange
        var host = CreateHost(out var middleware);
        var initialCount = middleware.Count;

        // Act
        _ = host.AddCommonAccessLog(opts => opts.IncludeQueryString = false);
        var result = host.AddCommonAccessLog(opts => opts.IncludeQueryString = true);

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware.Count > initialCount);
    }

    #endregion

    #region AddResponseCaching Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddResponseCaching_WithDelegateAndNoCacheControl_RegistersMiddleware()
    {
        // Arrange
        var host = CreateHost(out var middleware);

        // Act
        var result = host.AddResponseCaching(opts =>
        {
            opts.SizeLimit = 2048;
        });

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddResponseCaching_WithDelegateAndCacheControl_RegistersMiddleware()
    {
        // Arrange
        var host = CreateHost(out var middleware);
        var cacheControl = new CacheControlHeaderValue
        {
            MaxAge = TimeSpan.FromHours(1),
            Public = true
        };

        // Act
        var result = host.AddResponseCaching(opts =>
        {
            opts.SizeLimit = 1048576;
        }, cacheControl);

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddResponseCaching_WithNullDelegate_UsesDefaults()
    {
        // Arrange
        var host = CreateHost(out var middleware);

        // Act - Explicitly cast to Action delegate to avoid ambiguity
        var result = host.AddResponseCaching((Action<ResponseCachingOptions>?)null);

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddResponseCaching_WithCacheControlButNoDelegate_AppliesCacheControl()
    {
        // Arrange
        var host = CreateHost(out var middleware);
        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(5) };

        // Act - Explicitly cast to Action delegate to avoid ambiguity
        var result = host.AddResponseCaching((Action<ResponseCachingOptions>?)null, cacheControl);

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware.Count > 0);
    }

    #endregion

    #region AddHttpsRedirection Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHttpsRedirection_WithDelegate_RegistersMiddleware()
    {
        // Arrange
        var host = CreateHost(out var middleware);

        // Act
        var result = host.AddHttpsRedirection(opts =>
        {
            opts.HttpsPort = 8443;
        });

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHttpsRedirection_WithNullDelegate_UsesDefaults()
    {
        // Arrange
        var host = CreateHost(out var middleware);

        // Act - Explicitly cast to action delegate to avoid ambiguity
        var result = host.AddHttpsRedirection((Action<HttpsRedirectionOptions>?)null);

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHttpsRedirection_WithEmptyDelegate_UsesDefaults()
    {
        // Arrange
        var host = CreateHost(out var middleware);

        // Act
        var result = host.AddHttpsRedirection(_ => { });

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware.Count > 0);
    }

    #endregion

    #region AddHsts Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHsts_WithNullDelegate_RegistersMiddleware()
    {
        // Arrange
        var host = CreateHost(out var middleware);

        // Act - Explicitly cast to Action delegate to avoid ambiguity
        var result = host.AddHsts((Action<HstsOptions>?)null);

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHsts_WithDelegate_RegistersMiddleware()
    {
        // Arrange
        var host = CreateHost(out var middleware);

        // Act
        var result = host.AddHsts(opts =>
        {
            opts.MaxAge = TimeSpan.FromDays(365);
            opts.Preload = true;
        });

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddHsts_WithEmptyDelegate_UsesDefaults()
    {
        // Arrange
        var host = CreateHost(out var middleware);

        // Act
        var result = host.AddHsts(_ => { });

        // Assert
        Assert.NotNull(result);
        Assert.True(middleware.Count > 0);
    }

    #endregion

    #region Internal Helper Method Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateCachingInput_WithNullInputs_Succeeds()
    {
        // Arrange
        var host = CreateHost(out _);

        // Act & Assert - Should not throw
        KestrunHttpMiddlewareExtensions.ValidateCachingInput(host, null, null);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateCachingInput_WithCacheControl_SetDefaultCacheControl()
    {
        // Arrange
        var host = CreateHost(out _);
        var cacheControl = new CacheControlHeaderValue
        {
            MaxAge = TimeSpan.FromMinutes(10),
            Public = true
        };

        // Act
        KestrunHttpMiddlewareExtensions.ValidateCachingInput(host, null, cacheControl);

        // Assert
        Assert.NotNull(host.DefaultCacheControl);
        Assert.Equal(cacheControl.ToString(), host.DefaultCacheControl.ToString());
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ValidateCachingInput_WithConfigDelegate_Succeeds()
    {
        // Arrange
        var host = CreateHost(out _);
        var delegateCalled = false;
        void cfg(ResponseCachingOptions opts)
        {
            delegateCalled = true;
        }

        // Act
        KestrunHttpMiddlewareExtensions.ValidateCachingInput(host, cfg, null);

        // Assert - Just verifies no exception thrown
        Assert.False(delegateCalled); // Delegate should not be called by ValidateCachingInput
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void RegisterCachingServices_WithDelegate_RegistersServices()
    {
        // Arrange
        var host = CreateHost(out _);

        // Act & Assert - Should not throw
        KestrunHttpMiddlewareExtensions.RegisterCachingServices(host, opts =>
        {
            opts.SizeLimit = 2048;
        });
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void RegisterCachingServices_WithNullDelegate_RegistersDefaultServices()
    {
        // Arrange
        var host = CreateHost(out _);

        // Act & Assert - Should not throw
        KestrunHttpMiddlewareExtensions.RegisterCachingServices(host, null);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyCacheHeaders_WithGetRequestAndValidStatus_AppliesHeaders()
    {
        // Arrange
        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Response.StatusCode = 200;
        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(5) };

        // Act
        var result = KestrunHttpMiddlewareExtensions.ApplyCacheHeaders(context, cacheControl, logger.Object);

        // Assert
        Assert.True(result);
        Assert.True(context.Response.Headers.ContainsKey(HeaderNames.CacheControl));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyCacheHeaders_WithPostRequest_DoesNotApplyHeaders()
    {
        // Arrange
        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(false);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Response.StatusCode = 200;
        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(5) };

        // Act
        var result = KestrunHttpMiddlewareExtensions.ApplyCacheHeaders(context, cacheControl, logger.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyCacheHeaders_With404Status_DoesNotApplyHeaders()
    {
        // Arrange
        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(false);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Response.StatusCode = 404;
        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(5) };

        // Act
        var result = KestrunHttpMiddlewareExtensions.ApplyCacheHeaders(context, cacheControl, logger.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyCacheHeaders_WithSetCookieHeader_DoesNotApplyHeaders()
    {
        // Arrange
        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(false);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Response.StatusCode = 200;
        context.Response.Headers.Append(HeaderNames.SetCookie, "sessionid=123");
        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(5) };

        // Act
        var result = KestrunHttpMiddlewareExtensions.ApplyCacheHeaders(context, cacheControl, logger.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyCacheHeaders_WithNullCacheControl_DoesNotApplyHeaders()
    {
        // Arrange
        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(false);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Response.StatusCode = 200;

        // Act
        var result = KestrunHttpMiddlewareExtensions.ApplyCacheHeaders(context, null, logger.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void ApplyCacheHeaders_WithHeadRequest_AppliesHeaders()
    {
        // Arrange
        var logger = new Mock<Serilog.ILogger>();
        _ = logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(false);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Head;
        context.Response.StatusCode = 200;
        var cacheControl = new CacheControlHeaderValue { Public = true };

        // Act
        var result = KestrunHttpMiddlewareExtensions.ApplyCacheHeaders(context, cacheControl, logger.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CreateCachingMiddleware_WithCacheControl_CreatesValidMiddleware()
    {
        // Arrange
        var host = CreateHost(out _);
        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(5) };

        // Act
        var middleware = KestrunHttpMiddlewareExtensions.CreateCachingMiddleware(host, cacheControl);

        // Assert
        Assert.NotNull(middleware);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CreateCachingMiddleware_WithNullCacheControl_CreatesValidMiddleware()
    {
        // Arrange
        var host = CreateHost(out _);

        // Act
        var middleware = KestrunHttpMiddlewareExtensions.CreateCachingMiddleware(host, null);

        // Assert
        Assert.NotNull(middleware);
    }

    #endregion
}
