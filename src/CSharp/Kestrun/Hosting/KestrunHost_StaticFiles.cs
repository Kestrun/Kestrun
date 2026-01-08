
using Kestrun.Middleware;
using Microsoft.Net.Http.Headers;
using Serilog.Events;

namespace Kestrun.Hosting;

/// <summary>
/// Provides methods for configuring static file, default file, favicon, and file server middleware in Kestrun
/// </summary>
public partial class KestrunHost
{
    /// <summary>
    /// Adds default files middleware to the application.
    /// This middleware serves default files like index.html when a directory is requested.
    /// </summary>
    /// <param name="cfg">Configuration options for the default files middleware.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddDefaultFiles(DefaultFilesOptions? cfg)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding Default Files with configuration: {@Config}", cfg);
        }

        if (cfg == null)
        {
            return AddDefaultFiles(); // no config, use defaults
        }

        // Convert DefaultFilesOptions to an Action<DefaultFilesOptions>
        return AddDefaultFiles(options =>
        {
            CopyDefaultFilesOptions(cfg, options);
        });
    }

    /// <summary>
    /// Adds default files middleware to the application.
    /// This middleware serves default files like index.html when a directory is requested.
    /// </summary>
    /// <param name="cfg">Configuration options for the default files middleware.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddDefaultFiles(Action<DefaultFilesOptions>? cfg = null)
    {
        return Use(app =>
        {
            var options = new DefaultFilesOptions();
            cfg?.Invoke(options);
            _ = app.UseDefaultFiles(options);
        });
    }

    /// <summary>
    /// Adds a directory browser middleware to the application.
    /// This middleware enables directory browsing for a specified request path.
    /// </summary>
    /// <param name="requestPath">The request path to enable directory browsing for.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddDirectoryBrowser(string requestPath)
    {
        return Use(app =>
        {
            _ = app.UseDirectoryBrowser(requestPath);
        });
    }

    /// <summary>
    /// Adds a favicon middleware to the application.
    /// </summary>
    /// <param name="iconPath">The path to the favicon file. If null, uses the default favicon.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddFavicon(string? iconPath = null)
    {
        return Use(app =>
        {
            _ = app.UseFavicon(iconPath);
        });
    }


    /// <summary>
    /// Copies static file options from one object to another.
    /// </summary>
    /// <param name="src">The source static file options.</param>
    /// <param name="dest">The destination static file options.</param>
    /// <remarks>
    /// This method copies properties from the source static file options to the destination static file options.
    /// </remarks>
    private static void CopyStaticFileOptions(StaticFileOptions? src, StaticFileOptions dest)
    {
        // If no source, return a new empty options object
        if (src == null || dest == null)
        {
            return;
        }
        // Copy properties from source to destination
        dest.ContentTypeProvider = src.ContentTypeProvider;
        dest.OnPrepareResponse = src.OnPrepareResponse;
        dest.ServeUnknownFileTypes = src.ServeUnknownFileTypes;
        dest.DefaultContentType = src.DefaultContentType;
        dest.FileProvider = src.FileProvider;
        dest.RequestPath = src.RequestPath;
        dest.RedirectToAppendTrailingSlash = src.RedirectToAppendTrailingSlash;
        dest.HttpsCompression = src.HttpsCompression;
    }

    /// <summary>
    /// Copies default files options from one object to another.
    /// This method is used to ensure that the default files options are correctly configured.
    /// </summary>
    /// <param name="src">The source default files options.</param>
    /// <param name="dest">The destination default files options.</param>
    /// <remarks>
    /// This method copies properties from the source default files options to the destination default files options.
    /// </remarks>
    private static void CopyDefaultFilesOptions(DefaultFilesOptions? src, DefaultFilesOptions dest)
    {
        // If no source, return a new empty options object
        if (src == null || dest == null)
        {
            return;
        }
        // Copy properties from source to destination
        dest.DefaultFileNames.Clear();
        foreach (var name in src.DefaultFileNames)
        {
            dest.DefaultFileNames.Add(name);
        }

        dest.FileProvider = src.FileProvider;
        dest.RequestPath = src.RequestPath;
        dest.RedirectToAppendTrailingSlash = src.RedirectToAppendTrailingSlash;
    }

    /// <summary>
    /// Adds a file server middleware to the application.
    /// </summary>
    /// <param name="cfg">Configuration options for the file server middleware.</param>
    /// <param name="cacheControl">Optional cache control headers to apply to static file responses.</param>
    /// <returns>The current KestrunHost instance.</returns>
    /// <remarks>
    /// This middleware serves static files and default files from a specified file provider.
    /// If no configuration is provided, it uses default settings.
    /// </remarks>
    public KestrunHost AddFileServer(FileServerOptions? cfg, CacheControlHeaderValue? cacheControl = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding File Server with configuration: {@Config}", cfg);
        }

        if (cfg == null)
        {
            return AddFileServer(); // no config, use defaults
        }

        // Convert FileServerOptions to an Action<FileServerOptions>
        return AddFileServer(options =>
        {
            options.EnableDefaultFiles = cfg.EnableDefaultFiles;
            options.EnableDirectoryBrowsing = cfg.EnableDirectoryBrowsing;
            options.FileProvider = cfg.FileProvider;
            options.RequestPath = cfg.RequestPath;
            options.RedirectToAppendTrailingSlash = cfg.RedirectToAppendTrailingSlash;
            CopyDefaultFilesOptions(cfg.DefaultFilesOptions, options.DefaultFilesOptions);
            if (cfg.DirectoryBrowserOptions != null)
            {
                options.DirectoryBrowserOptions.FileProvider = cfg.DirectoryBrowserOptions.FileProvider;
                options.DirectoryBrowserOptions.RequestPath = cfg.DirectoryBrowserOptions.RequestPath;
                options.DirectoryBrowserOptions.RedirectToAppendTrailingSlash = cfg.DirectoryBrowserOptions.RedirectToAppendTrailingSlash;
            }

            CopyStaticFileOptions(cfg.StaticFileOptions, options.StaticFileOptions);
            if (cacheControl != null)
            {
                options.StaticFileOptions.OnPrepareResponse = ctx =>
                {
                    // Apply the provided cache control if one was given
                    if (Logger.IsEnabled(LogEventLevel.Debug))
                    {
                        Logger.Debug("Setting Cache-Control header to: {@CacheControl}", cacheControl);
                    }
                    ctx.Context.Response.Headers.CacheControl = cacheControl.ToString();
                };
            }
            else if (DefaultCacheControl != null)
            {
                options.StaticFileOptions.OnPrepareResponse = ctx =>
                {
                    // Apply the host-wide default cache control if no specific one was provided
                    if (Logger.IsEnabled(LogEventLevel.Debug))
                    {
                        Logger.Debug("Setting host-wide default cache Cache-Control: {@DefaultCacheControl}", DefaultCacheControl);
                    }
                    ctx.Context.Response.Headers.CacheControl = DefaultCacheControl.ToString();
                };
            }
        });
    }

    /// <summary>
    /// Adds a file server middleware to the application.
    /// This middleware serves static files and default files from a specified file provider.
    /// </summary>
    /// <param name="cfg">Configuration options for the file server middleware.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddFileServer(Action<FileServerOptions>? cfg = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding File Server with Action<configuration>");
        }

        return Use(app =>
        {
            var options = new FileServerOptions();
            cfg?.Invoke(options);
            _ = app.UseFileServer(options);
        });
    }

    /// <summary>
    /// Adds static files to the application.
    /// This overload allows you to specify configuration options.
    /// </summary>
    /// <param name="cfg">The static file options to configure.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddStaticFiles(Action<StaticFileOptions>? cfg = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding static files with configuration: {Config}", cfg);
        }

        return Use(app =>
        {
            if (cfg == null)
            {
                _ = app.UseStaticFiles();
            }
            else
            {
                var options = new StaticFileOptions();
                cfg(options);

                _ = app.UseStaticFiles(options);
            }
        });
    }

    /// <summary>
    /// Adds static files to the application.
    /// This overload allows you to specify configuration options.
    /// </summary>
    /// <param name="options">The static file options to configure.</param>
    /// <param name="cacheControl">Optional cache control headers to apply to static file responses.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddStaticFiles(StaticFileOptions options, CacheControlHeaderValue? cacheControl = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding static files with options: {@Options} and cache control: {@CacheControl}", options, cacheControl);
        }

        if (options == null)
        {
            return AddStaticFiles(); // no options, use defaults
        }

        // reuse the delegate overload so the pipeline logic stays in one place
        return AddStaticFiles(o =>
        {
            // copy only the properties callers are likely to set
            CopyStaticFileOptions(options, o);
            if (cacheControl != null)
            {
                o.OnPrepareResponse = ctx =>
                {
                    // Apply the provided cache control if one was given
                    if (Logger.IsEnabled(LogEventLevel.Debug))
                    {
                        Logger.Debug("Setting Cache-Control header to: {@CacheControl}", cacheControl);
                    }
                    ctx.Context.Response.Headers.CacheControl = cacheControl.ToString();
                };
            }
            else if (DefaultCacheControl != null)
            {
                o.OnPrepareResponse = ctx =>
                {
                    // Apply the host-wide default cache control if no specific one was provided
                    if (Logger.IsEnabled(LogEventLevel.Debug))
                    {
                        Logger.Debug("Setting host-wide default cache Cache-Control: {@DefaultCacheControl}", DefaultCacheControl);
                    }
                    ctx.Context.Response.Headers.CacheControl = DefaultCacheControl.ToString();
                };
            }
        });
    }
}
