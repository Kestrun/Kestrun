using Kestrun.Middleware;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Net.Http.Headers;
using Serilog.Events;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Localization;

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
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug(
                "Adding common access log middleware (custom configuration supplied: {HasConfig})",
                configure != null);
        }

        _ = host.AddService(services =>
        {
            // Ensure a Serilog.ILogger is available for middleware constructor injection.
            // We don't overwrite a user-provided registration.
            services.TryAddSingleton(_ => host.Logger);

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
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding response compression with options: {@Options}", options);
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
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding response compression with configuration: {HasConfig}", cfg != null);
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
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding response caching with options: {@Options}", options);
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
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding response caching with Action<ResponseCachingOptions>{HasConfig} and Cache-Control: {HasCacheControl}", cfg != null, cacheControl != null);
        }

        // Remember the default Cache-Control if provided
        if (cacheControl is not null)
        {
            host.Logger.Information("Setting default Cache-Control: {CacheControl}", cacheControl.ToString());
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
                    _ = ApplyCacheHeaders(context, cacheControl, host.Logger);
                }
                catch (Exception ex)
                {
                    // Never let caching decoration break the response
                    host.Logger.Warning(ex, "Failed to apply default cache headers.");
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
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding HTTPS redirection with configuration: {@Config}", cfg);
        }

        if (cfg == null)
        {
            return host.AddHttpsRedirection();   // fallback to parameterless overload
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
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding HTTPS redirection with configuration: {HasConfig}", cfg != null);
        }

        // Register the HTTPS redirection service
        _ = host.AddService(services =>
            {
                _ = services.AddHttpsRedirection(cfg ?? (_ => { })); // Always pass a delegate
            });

        // Apply the middleware
        return host.Use(app => app.UseHttpsRedirection());
    }


    /// <summary>
    /// Adds HSTS to the application using the specified <see cref="HstsOptions"/>.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="opts">The delegate for configuring HSTS options.</param>
    /// <returns>The updated KestrunHost instance.</returns>
    public static KestrunHost AddHsts(this KestrunHost host, Action<HstsOptions>? opts = null)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding HSTS with configuration: {HasConfig}", opts != null);
        }

        // Register the HSTS service
        _ = host.AddService(services =>
            {
                _ = services.AddHsts(opts ?? (_ => { })); // Always pass a delegate
            });

        // Apply the middleware
        return host.Use(app => app.UseHsts());
    }

    /// <summary>
    /// Adds HSTS to the application using the specified <see cref="HstsOptions"/>.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="opts">The HSTS options.</param>
    /// <returns>The updated KestrunHost instance.</returns>
    public static KestrunHost AddHsts(this KestrunHost host, HstsOptions opts)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding HSTS with configuration: {@Config}", opts);
        }

        if (opts == null)
        {
            return host.AddHsts();   // fallback to parameterless overload
        }

        _ = host.AddService(services =>
        {
            _ = services.AddHsts(o =>
            {
                o.Preload = opts.Preload;
                o.IncludeSubDomains = opts.IncludeSubDomains;
                o.MaxAge = opts.MaxAge;
                o.ExcludedHosts.Clear();
                foreach (var h in opts.ExcludedHosts)
                {
                    o.ExcludedHosts.Add(h);
                }
            });
        });

        return host.Use(app => app.UseHsts());
    }

    /// <summary>
    /// Adds request localization middleware using the specified <see cref="RequestLocalizationOptions"/>.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="opts">The request localization options.</param>
    /// <returns>The updated KestrunHost instance.</returns>
    public static KestrunHost AddRequestLocalization(this KestrunHost host, RequestLocalizationOptions opts)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding request localization with configuration: {@Config}", opts);
        }

        if (opts == null)
        {
            return host.AddRequestLocalization();   // fallback to parameterless overload
        }

        _ = host.AddService(services =>
        {
            _ = services.Configure<RequestLocalizationOptions>(o =>
            {
                o.DefaultRequestCulture = opts.DefaultRequestCulture;
                o.SupportedCultures = opts.SupportedCultures;
                o.SupportedUICultures = opts.SupportedUICultures;
                o.RequestCultureProviders = opts.RequestCultureProviders;
                o.FallBackToParentCultures = opts.FallBackToParentCultures;
                o.FallBackToParentUICultures = opts.FallBackToParentUICultures;
                o.ApplyCurrentCultureToResponseHeaders = opts.ApplyCurrentCultureToResponseHeaders;
            });
        });

        return host.Use(app => app.UseRequestLocalization(opts));
    }

    /// <summary>
    /// Adds request localization middleware using the specified configuration delegate.
    /// </summary>
    /// <param name="host">The KestrunHost instance to configure.</param>
    /// <param name="cfg">The configuration delegate for request localization options.</param>
    /// <returns>The updated KestrunHost instance.</returns>
    public static KestrunHost AddRequestLocalization(this KestrunHost host, Action<RequestLocalizationOptions>? cfg = null)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding request localization with configuration: {HasConfig}", cfg != null);
        }

        var options = new RequestLocalizationOptions();
        
        if (cfg != null)
        {
            cfg(options);
        }

        // Register the request localization service
        _ = host.AddService(services =>
        {
            _ = services.Configure<RequestLocalizationOptions>(cfg ?? (_ => { }));
        });

        // Apply the middleware
        return host.Use(app => app.UseRequestLocalization(options));
    }
}
