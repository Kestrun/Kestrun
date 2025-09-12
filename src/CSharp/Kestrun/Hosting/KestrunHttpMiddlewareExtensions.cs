using Kestrun.Utilities;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Serilog.Events;

namespace Kestrun.Hosting;

/// <summary>
/// Provides extension methods for configuring common HTTP middleware in Kestrun.
/// </summary>
public static class KestrunHttpMiddlewareExtensions
{
    /// <summary>
    /// Adds response compression to the application.
    /// This overload allows you to specify configuration options.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="options">The configuration options for response compression.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public static KestrunHost AddResponseCompression(this KestrunHost host, ResponseCompressionOptions? options)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("Adding response compression with options: {@Options}", options);
        }

        if (options == null)
        {
            return host.AddResponseCompression(); // no options, use defaults
        }

        // delegate shim – re‑use the existing pipeline
        return host.AddResponseCompression(o =>
        {
            o.EnableForHttps = options.EnableForHttps;
            o.MimeTypes = options.MimeTypes;
            o.ExcludedMimeTypes = options.ExcludedMimeTypes;
            // copy provider lists, levels, etc. if you expose them
            foreach (var p in options.Providers)
            {
                o.Providers.Add(p);
            }
        });
    }

    /// <summary>
    /// Adds response compression to the application.
    /// This overload allows you to specify configuration options.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="cfg">The configuration options for response compression.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public static KestrunHost AddResponseCompression(this KestrunHost host, Action<ResponseCompressionOptions>? cfg = null)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("Adding response compression with configuration: {Config}", cfg);
        }
        // Service side
        _ = host.AddService(services =>
        {
            _ = cfg == null ? services.AddResponseCompression() : services.AddResponseCompression(cfg);
        });

        // Middleware side
        return host.Use(app => app.UseResponseCompression());
    }

    /// <summary>
    /// Adds rate limiting to the application using the specified <see cref="RateLimiterOptions"/>.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="cfg">The configuration options for rate limiting.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public static KestrunHost AddRateLimiter(this KestrunHost host, RateLimiterOptions cfg)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("Adding rate limiter with configuration: {@Config}", cfg);
        }

        if (cfg == null)
        {
            return host.AddRateLimiter();   // fall back to your “blank” overload
        }

        _ = host.AddService(services =>
        {
            _ = services.AddRateLimiter(opts => opts.CopyFrom(cfg));   // ← single line!
        });

        return host.Use(app => app.UseRateLimiter());
    }


    /// <summary>
    /// Adds rate limiting to the application using the specified configuration delegate.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="cfg">An optional delegate to configure rate limiting options.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public static KestrunHost AddRateLimiter(this KestrunHost host, Action<RateLimiterOptions>? cfg = null)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("Adding rate limiter with configuration: {HasConfig}", cfg != null);
        }

        // Register the rate limiter service
        _ = host.AddService(services =>
            {
                _ = services.AddRateLimiter(cfg ?? (_ => { })); // Always pass a delegate
            });

        // Apply the middleware
        return host.Use(app =>
        {
            if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
            {
                host.HostLogger.Debug("Registering rate limiter middleware");
            }

            _ = app.UseRateLimiter();
        });
    }



    /// <summary>
    /// Adds antiforgery protection to the application.
    /// This overload allows you to specify configuration options.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="options">The antiforgery options to configure.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public static KestrunHost AddAntiforgery(this KestrunHost host, AntiforgeryOptions? options)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("Adding Antiforgery with configuration: {@Config}", options);
        }

        if (options == null)
        {
            return host.AddAntiforgery(); // no config, use defaults
        }

        // Delegate to the Action-based overload
        return host.AddAntiforgery(cfg =>
        {
            cfg.Cookie = options.Cookie;
            cfg.FormFieldName = options.FormFieldName;
            cfg.HeaderName = options.HeaderName;
            cfg.SuppressXFrameOptionsHeader = options.SuppressXFrameOptionsHeader;
        });
    }

    /// <summary>
    /// Adds antiforgery protection to the application.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="setupAction">An optional action to configure the antiforgery options.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public static KestrunHost AddAntiforgery(this KestrunHost host, Action<AntiforgeryOptions>? setupAction = null)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("Adding Antiforgery with configuration: {@Config}", setupAction);
        }
        // Service side
        _ = host.AddService(services =>
        {
            _ = setupAction == null ? services.AddAntiforgery() : services.AddAntiforgery(setupAction);
        });

        // Middleware side
        return host.Use(app => app.UseAntiforgery());
    }


    /// <summary>
    /// Adds a CORS policy named "AllowAll" that allows any origin, method, and header.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public static KestrunHost AddCorsAllowAll(this KestrunHost host) =>
        host.AddCors("AllowAll", b => b.AllowAnyOrigin()
                                  .AllowAnyMethod()
                                  .AllowAnyHeader());

    /// <summary>
    /// Registers a named CORS policy that was already composed with a
    /// <see cref="CorsPolicyBuilder"/> and applies that policy in the pipeline.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="policyName">The name to store/apply the policy under.</param>
    /// <param name="builder">
    ///     A fully‑configured <see cref="CorsPolicyBuilder"/>.
    ///     Callers typically chain <c>.WithOrigins()</c>, <c>.WithMethods()</c>,
    ///     etc. before passing it here.
    /// </param>
    public static KestrunHost AddCors(this KestrunHost host, string policyName, CorsPolicyBuilder builder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(builder);

        // 1️⃣ Service‑time registration
        _ = host.AddService(services =>
        {
            _ = services.AddCors(options =>
            {
                options.AddPolicy(policyName, builder.Build());
            });
        });

        // 2️⃣ Middleware‑time application
        return host.Use(app => app.UseCors(policyName));
    }

    /// <summary>
    /// Registers a named CORS policy that was already composed with a
    /// <see cref="CorsPolicyBuilder"/> and applies that policy in the pipeline.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="policyName">The name to store/apply the policy under.</param>
    /// <param name="buildPolicy">An action to configure the CORS policy.</param>
    /// <returns>The current KestrunHost instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the policy name is null or whitespace.</exception>
    public static KestrunHost AddCors(this KestrunHost host, string policyName, Action<CorsPolicyBuilder> buildPolicy)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("Adding CORS policy: {PolicyName}", policyName);
        }

        if (string.IsNullOrWhiteSpace(policyName))
        {
            throw new ArgumentException("Policy name required.", nameof(policyName));
        }

        ArgumentNullException.ThrowIfNull(buildPolicy);

        _ = host.AddService(s =>
        {
            _ = s.AddCors(o => o.AddPolicy(policyName, buildPolicy));
        });

        // apply only that policy
        return host.Use(app => app.UseCors(policyName));
    }
}
