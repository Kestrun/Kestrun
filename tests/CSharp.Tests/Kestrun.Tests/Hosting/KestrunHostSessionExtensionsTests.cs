using Kestrun.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Microsoft.Extensions.Caching.Distributed;

namespace KestrunTests.Hosting;

public class KestrunHostSessionExtensionsTests
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

    #region Session and Distributed Cache Tests

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddSession_WithNullOptions_RegistersMiddleware()
    {
        var host = CreateHost(out var middleware);
        _ = host.AddSession(null!);
        Assert.True(middleware.Count > 0);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddDistributedMemoryCache_WithNullOptions_RegistersService()
    {
        var host = CreateHost(out _);
        _ = host.AddDistributedMemoryCache(null!);

        // Extract queued service registrations and build a provider to validate registration
        var serviceField = typeof(KestrunHost).GetField("_serviceQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var actions = (List<Action<IServiceCollection>>)serviceField!.GetValue(host)!;

        var services = new ServiceCollection();
        foreach (var a in actions)
        {
            a(services);
        }
        using var sp = services.BuildServiceProvider();
        var cache = sp.GetService<IDistributedCache>();
        Assert.NotNull(cache);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddStackExchangeRedisCache_WithOptions_RegistersService()
    {
        var host = CreateHost(out _);

        // Skip test if UPSTASH_REDIS_URL is not available
        var url = Environment.GetEnvironmentVariable("UPSTASH_REDIS_URL");
        if (string.IsNullOrEmpty(url))
        {
            return; // Skip test when Redis URL is not configured
        }

        var options = new Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions
        {
            Configuration = url,
            InstanceName = "KestrunTest_"
        };

        _ = host.AddStackExchangeRedisCache(options);

        // Extract and apply queued service registrations
        var serviceField = typeof(KestrunHost).GetField("_serviceQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var actions = (List<Action<IServiceCollection>>)serviceField!.GetValue(host)!;

        var services = new ServiceCollection();
        foreach (var a in actions)
        {
            a(services);
        }
        using var sp = services.BuildServiceProvider();
        var cache = sp.GetService<IDistributedCache>();
        Assert.NotNull(cache);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddDistributedSqlServerCache_WithOptions_RegistersService()
    {
        var host = CreateHost(out _);

        var options = new Microsoft.Extensions.Caching.SqlServer.SqlServerCacheOptions
        {
            ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;",
            SchemaName = "dbo",
            TableName = "KestrunSessionCache",
            ExpiredItemsDeletionInterval = TimeSpan.FromMinutes(30),
            DefaultSlidingExpiration = TimeSpan.FromMinutes(20)
        };

        _ = host.AddDistributedSqlServerCache(options);

        // Extract and apply queued service registrations
        var serviceField = typeof(KestrunHost).GetField("_serviceQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var actions = (List<Action<IServiceCollection>>)serviceField!.GetValue(host)!;

        var services = new ServiceCollection();
        foreach (var a in actions)
        {
            a(services);
        }
        using var sp = services.BuildServiceProvider();
        var cache = sp.GetService<IDistributedCache>();
        Assert.NotNull(cache);
    }

    #endregion
}
