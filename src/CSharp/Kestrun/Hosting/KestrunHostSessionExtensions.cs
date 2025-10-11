using Serilog.Events;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Caching.SqlServer;
using Microsoft.Extensions.Options;

namespace Kestrun.Hosting;

/// <summary>
/// Provides extension methods for configuring common HTTP middleware in Kestrun.
/// </summary>
public static class KestrunHostSessionExtensions
{
    /// <summary>
    /// Adds session state services and middleware to the application.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost" /> to add services to.</param>
    /// <param name="cfg">The configuration options for session state.</param>
    /// <returns>The updated <see cref="KestrunHost" /> instance.</returns>
    public static KestrunHost AddSession(this KestrunHost host, SessionOptions? cfg)
    {
        // Validate parameters
        ArgumentNullException.ThrowIfNull(host);

        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding Session with configuration: {@Config}", cfg);
        }
        // Avoid adding multiple session middlewares
        if (host.IsServiceRegistered(typeof(IConfigureOptions<SessionOptions>)))
        {
            throw new InvalidOperationException("Session services are already registered. Only one session configuration can be registered per host.");
        }

        // Add the session services
        _ = host.AddService(services =>
        {
            _ = (cfg is null) ?
                services.AddSession() :
                services.AddSession(opts =>
                 {
                     opts.Cookie = cfg.Cookie;
                     opts.IdleTimeout = cfg.IdleTimeout;
                     opts.IOTimeout = cfg.IOTimeout;
                 });
        });

        return host.Use(app => app.UseSession());
    }

    /// <summary>
    /// Checks if a distributed cache implementation is already registered with the host.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost" /> to check.</param>
    /// <returns>true if a distributed cache is registered; otherwise, false.</returns>
    public static bool IsDistributedCacheRegistered(this KestrunHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.IsServiceRegistered(typeof(IDistributedCache));
    }

    /// <summary>
    /// Adds a default implementation of <see cref="IDistributedCache"/> that stores items in memory
    /// to the <see cref="KestrunHost" />. Frameworks that require a distributed cache to work
    /// can safely add this dependency as part of their dependency list to ensure that there is at least
    /// one implementation available.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost" /> to add services to.</param>
    /// <param name="cfg">The configuration options for the memory distributed cache.</param>
    /// <returns>The <see cref="KestrunHost"/> so that additional calls can be chained.</returns>
    public static KestrunHost AddDistributedMemoryCache(this KestrunHost host, MemoryDistributedCacheOptions? cfg)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding Distributed Memory Cache with configuration: {@Config}", cfg);
        }

        // Avoid adding multiple distributed cache implementations
        if (IsDistributedCacheRegistered(host))
        {
            throw new InvalidOperationException("A distributed cache implementation is already registered. Only one distributed cache can be registered per host.");
        }

        // Add the distributed memory cache service
        return host.AddService(services =>
        {
            _ = (cfg is null) ?
                services.AddDistributedMemoryCache() :
                services.AddDistributedMemoryCache(opts =>
                {
                    opts.Clock = cfg.Clock;
                    opts.CompactionPercentage = cfg.CompactionPercentage;
                    opts.ExpirationScanFrequency = cfg.ExpirationScanFrequency;
                    opts.SizeLimit = cfg.SizeLimit;
                    opts.TrackLinkedCacheEntries = cfg.TrackLinkedCacheEntries;
                    opts.TrackStatistics = cfg.TrackStatistics;
                });
        });
    }

    /// <summary>
    /// Adds a StackExchange Redis implementation of <see cref="IDistributedCache"/> to the <see cref="KestrunHost" />.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost" /> to add services to.</param>
    /// <param name="cfg">The configuration options for the Redis cache.</param>
    /// <returns>The updated <see cref="KestrunHost" /> instance.</returns>
    public static KestrunHost AddStackExchangeRedisCache(this KestrunHost host, RedisCacheOptions cfg)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(cfg);
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding StackExchange Redis Cache with configuration: {@Config}", cfg);
        }

        // Avoid adding multiple distributed cache implementations
        if (host.IsDistributedCacheRegistered())
        {
            throw new InvalidOperationException("A distributed cache implementation is already registered. Only one distributed cache can be registered per host.");
        }

        // Ensure that the ConnectionMultiplexerFactory is set to avoid issues with multiple registrations
        return host.AddService(services =>
        {
            _ = services.AddStackExchangeRedisCache(opts =>
                {
                    opts.Configuration = cfg.Configuration;
                    opts.ConfigurationOptions = cfg.ConfigurationOptions;
                    opts.InstanceName = cfg.InstanceName;
                    opts.ProfilingSession = cfg.ProfilingSession;
                    opts.ConnectionMultiplexerFactory = cfg.ConnectionMultiplexerFactory;
                });
        });
    }

    /// <summary>
    /// Adds a SQL Server implementation of <see cref="IDistributedCache"/> to the <see cref="KestrunHost" />.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost" /> to add services to.</param>
    /// <param name="cfg">The configuration options for the SQL Server cache.</param>
    /// <returns>The updated <see cref="KestrunHost" /> instance.</returns>
    public static KestrunHost AddDistributedSqlServerCache(this KestrunHost host, SqlServerCacheOptions cfg)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(cfg);
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding Distributed SQL Server Cache with configuration: {@Config}", cfg);
        }

        // Avoid adding multiple distributed cache implementations
        if (host.IsDistributedCacheRegistered())
        {
            throw new InvalidOperationException("A distributed cache implementation is already registered. Only one distributed cache can be registered per host.");
        }

        // Ensure that the ConnectionMultiplexerFactory is set to avoid issues with multiple registrations
        return host.AddService(services =>
        {
            _ = services.AddDistributedSqlServerCache(opts =>
                {
                    opts.ConnectionString = cfg.ConnectionString;
                    opts.SchemaName = cfg.SchemaName;
                    opts.TableName = cfg.TableName;
                    opts.ExpiredItemsDeletionInterval = cfg.ExpiredItemsDeletionInterval;
                    opts.DefaultSlidingExpiration = cfg.DefaultSlidingExpiration;
                });
        });
    }
}
