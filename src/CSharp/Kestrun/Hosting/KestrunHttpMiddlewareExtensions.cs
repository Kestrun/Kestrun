using Kestrun.Middleware;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Net.Http.Headers;
using Serilog.Events;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.HttpsPolicy; // Added for TryAddSingleton

namespace Kestrun.Hosting;

/// <summary>
/// Provides extension methods for configuring common HTTP middleware in Kestrun.
/// </summary>
public static class KestrunHttpMiddlewareExtensions
{
    /// <summary>
    /// Adds Apache-style common access logging using a configured <see cref="CommonAccessLogOptions"/> instance.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost"/> instance to configure.</param>
    /// <param name="configure">Optional pre-configured <see cref="CommonAccessLogOptions"/> instance.</param>
    /// <returns>The configured <see cref="KestrunHost"/> instance.</returns>
    public static KestrunHost AddCommonAccessLog(this KestrunHost host, CommonAccessLogOptions configure)
    {
        return host.AddCommonAccessLog(opts =>
        {
            opts.Level = configure.Level;
            opts.IncludeQueryString = configure.IncludeQueryString;
            opts.IncludeProtocol = configure.IncludeProtocol;
            opts.IncludeElapsedMilliseconds = configure.IncludeElapsedMilliseconds;
            opts.UseUtcTimestamp = configure.UseUtcTimestamp;
            opts.TimestampFormat = configure.TimestampFormat;
            opts.ClientAddressHeader = configure.ClientAddressHeader;
            opts.TimeProvider = configure.TimeProvider;
            opts.Logger = configure.Logger;
        });
    }

    /// <summary>
    /// Adds Apache-style common access logging using <see cref="CommonAccessLogMiddleware"/>.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost"/> instance to configure.</param>
    /// <param name="configure">Optional delegate to configure <see cref="CommonAccessLogOptions"/>.</param>
    /// <returns>The configured <see cref="KestrunHost"/> instance.</returns>
    public static KestrunHost AddCommonAccessLog(this KestrunHost host, Action<CommonAccessLogOptions>? configure = null)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug(
                "Adding common access log middleware (custom configuration supplied: {HasConfig})",
                configure != null);
        }

        _ = host.AddService(services =>
        {
            // Ensure a Serilog.ILogger is available for middleware constructor injection.
            // We don't overwrite a user-provided registration.
            services.TryAddSingleton(_ => host.HostLogger);

            var builder = services.AddOptions<CommonAccessLogOptions>();
            if (configure != null)
            {
                _ = builder.Configure(configure);
            }
        });

        return host.Use(app => app.UseMiddleware<CommonAccessLogMiddleware>());
    }



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
            host.HostLogger.Debug("Adding response compression with configuration: {HasConfig}", cfg != null);
        }
        // Service side
        _ = host.AddService(services =>
        {
            _ = cfg == null ? services.AddResponseCompression() : services.AddResponseCompression(cfg);
            // replace the default provider with our opt-out decorator
            _ = services.AddSingleton<IResponseCompressionProvider, Compression.KestrunResponseCompressionProvider>();
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
#if NET9_0_OR_GREATER
            cfg.SuppressReadingTokenFromFormBody = options.SuppressReadingTokenFromFormBody;
#endif
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
            host.HostLogger.Debug(
                setupAction == null
                    ? "Adding Antiforgery with default configuration (no custom options provided)."
                    : "Adding Antiforgery with custom configuration via setupAction."
            );
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

    /// <summary>
    /// Adds response caching to the application.
    /// This overload allows you to specify configuration options.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="options">The configuration options for response caching.</param>
    /// <param name="cacheControl">
    /// Optional default Cache-Control to apply (only if the response didn't set one).
    /// </param>
    public static KestrunHost AddResponseCaching(this KestrunHost host, ResponseCachingOptions options, CacheControlHeaderValue? cacheControl = null)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("Adding response caching with options: {@Options}", options);
        }

        // delegate shim – re‑use the existing pipeline
        return host.AddResponseCaching(o =>
        {
            o.SizeLimit = options.SizeLimit;
            o.MaximumBodySize = options.MaximumBodySize;
            o.UseCaseSensitivePaths = options.UseCaseSensitivePaths;
        }, cacheControl);
    }

    /// <summary>
    /// Validates inputs and performs initial logging for response caching configuration.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="cfg">Optional configuration for response caching.</param>
    /// <param name="cacheControl">Optional default Cache-Control to apply.</param>
    internal static void ValidateCachingInput(KestrunHost host, Action<ResponseCachingOptions>? cfg, CacheControlHeaderValue? cacheControl)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("Adding response caching with Action<ResponseCachingOptions>{HasConfig} and Cache-Control: {HasCacheControl}", cfg != null, cacheControl != null);
        }

        // Remember the default Cache-Control if provided
        if (cacheControl is not null)
        {
            host.HostLogger.Information("Setting default Cache-Control: {CacheControl}", cacheControl.ToString());
            // Save for reference
            host.DefaultCacheControl = cacheControl;
        }
    }

    /// <summary>
    /// Registers response caching services with the dependency injection container.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="cfg">Optional configuration for response caching.</param>
    internal static void RegisterCachingServices(KestrunHost host, Action<ResponseCachingOptions>? cfg)
    {
        _ = host.AddService(services =>
        {
            _ = cfg == null ? services.AddResponseCaching() : services.AddResponseCaching(cfg);
        });
    }

    /// <summary>
    /// Applies cache control headers to the HTTP response if conditions are met.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="cacheControl">The cache control header value to apply.</param>
    /// <param name="logger">The Serilog logger instance for debugging.</param>
    /// <returns>True if headers were applied, false otherwise.</returns>
    internal static bool ApplyCacheHeaders(HttpContext context, CacheControlHeaderValue? cacheControl, Serilog.ILogger logger)
    {
        // Gate: only for successful cacheable responses on GET/HEAD
        var method = context.Request.Method;
        if (!(HttpMethods.IsGet(method) || HttpMethods.IsHead(method)))
        {
            return false;
        }

        var status = context.Response.StatusCode;
        if (status is < 200 or >= 300)
        {
            return false;
        }

        // ResponseCaching won't cache if Set-Cookie is present; don't add headers in that case
        if (context.Response.Headers.ContainsKey(HeaderNames.SetCookie))
        {
            return false;
        }

        // Only apply default Cache-Control if none was set and caller provided one
        if (cacheControl is not null)
        {
            context.Response.Headers.CacheControl = cacheControl.ToString();

            // If you expect compression variability elsewhere, add Vary only if absent
            if (!context.Response.Headers.ContainsKey(HeaderNames.Vary))
            {
                context.Response.Headers.Append(HeaderNames.Vary, "Accept-Encoding");
            }

            if (logger.IsEnabled(LogEventLevel.Debug))
            {
                logger.Debug("Applied default Cache-Control: {CacheControl}", cacheControl.ToString());
            }
            return true;
        }
        else
        {
            if (logger.IsEnabled(LogEventLevel.Debug))
            {
                logger.Debug("No default cache Control provided; skipping.");
            }
            return false;
        }
    }

    /// <summary>
    /// Creates the caching middleware that applies cache headers.
    /// </summary>
    /// <param name="host">The KestrunHost instance.</param>
    /// <param name="cacheControl">Optional cache control header value.</param>
    /// <returns>The configured middleware action.</returns>
    internal static Action<IApplicationBuilder> CreateCachingMiddleware(KestrunHost host, CacheControlHeaderValue? cacheControl)
    {
        return app =>
        {
            _ = app.UseResponseCaching();
            _ = app.Use(async (context, next) =>
            {
                try
                {
                    _ = ApplyCacheHeaders(context, cacheControl, host.HostLogger);
                }
                catch (Exception ex)
                {
                    // Never let caching decoration break the response
                    host.HostLogger.Warning(ex, "Failed to apply default cache headers.");
                }
                finally
                {
                    await next(context);
                }
            });
        };
    }

    /// <summary>
    /// Adds response caching to the application.
    /// This overload allows you to specify a configuration delegate.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="cfg">Optional configuration for response caching.</param>
    /// <param name="cacheControl">Optional default Cache-Control to apply (only if the response didn't set one).</param>
    /// <returns> The updated KestrunHost instance. </returns>
    public static KestrunHost AddResponseCaching(this KestrunHost host, Action<ResponseCachingOptions>? cfg = null,
        CacheControlHeaderValue? cacheControl = null)
    {
        ValidateCachingInput(host, cfg, cacheControl);
        RegisterCachingServices(host, cfg);
        return host.Use(CreateCachingMiddleware(host, cacheControl));
    }


    /// <summary>
    /// Adds HTTPS redirection to the application using the specified <see cref="HttpsRedirectionOptions"/>.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="cfg">The HTTPS redirection options.</param>
    /// <returns>The updated KestrunHost instance.</returns>
    public static KestrunHost AddHttpsRedirection(this KestrunHost host, HttpsRedirectionOptions cfg)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("Adding HTTPS redirection with configuration: {@Config}", cfg);
        }

        if (cfg == null)
        {
            return host.AddHttpsRedirection();   // fall back to your “blank” overload
        }

        _ = host.AddService(services =>
        {
            _ = services.AddHttpsRedirection(opts =>
            {
                opts.RedirectStatusCode = cfg.RedirectStatusCode;
                opts.HttpsPort = cfg.HttpsPort;
            });
        });

        return host.Use(app => app.UseHttpsRedirection());
    }

    /// <summary>
    /// Adds HTTPS redirection to the application using the specified configuration delegate.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="cfg">The configuration delegate for HTTPS redirection options.</param>
    /// <returns>The updated KestrunHost instance.</returns>
    public static KestrunHost AddHttpsRedirection(this KestrunHost host, Action<HttpsRedirectionOptions>? cfg = null)
    {
        if (host.HostLogger.IsEnabled(LogEventLevel.Debug))
        {
            host.HostLogger.Debug("Adding HTTPS redirection with configuration: {HasConfig}", cfg != null);
        }

        // Register the HTTPS redirection service
        _ = host.AddService(services =>
            {
                _ = services.AddHttpsRedirection(cfg ?? (_ => { })); // Always pass a delegate
            });

        // Apply the middleware
        return host.Use(app => app.UseHttpsRedirection());
    }

}
