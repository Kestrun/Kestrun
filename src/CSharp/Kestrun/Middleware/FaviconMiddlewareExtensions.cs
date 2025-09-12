// File: Middleware/FaviconMiddlewareExtensions.cs
using Microsoft.AspNetCore.StaticFiles;
using Serilog;

namespace Kestrun.Middleware;

/// <summary>
/// Provides extension methods for serving a favicon in ASP.NET Core applications.
/// </summary>
public static class FaviconMiddlewareExtensions
{
    /// <summary>
    /// Adds middleware to serve a favicon for the application.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="iconPath">Optional path to a custom favicon file. If not provided, uses the embedded favicon.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseFavicon(this IApplicationBuilder app, string? iconPath = null)
    {
        if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            Log.Debug("Using favicon middleware, iconPath={IconPath}", iconPath);
        }

        ArgumentNullException.ThrowIfNull(app);

        // MIME-type detection
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        string? contentType;
        byte[] iconBytes;

        // Check if user provided a custom icon path
        if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                Log.Debug("Using user-provided favicon at {IconPath}", iconPath);
            }
            // Serve user-provided file
            iconBytes = File.ReadAllBytes(iconPath);

            if (!contentTypeProvider.TryGetContentType(iconPath, out contentType) || contentType is null)
            {
                contentType = "application/octet-stream"; // fallback
            }
        }
        else
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                Log.Debug("Using embedded favicon, no custom path provided");
            }
            // Fallback to embedded .ico
            var asm = typeof(FaviconMiddlewareExtensions).Assembly;
            const string embedded = "Kestrun.Assets.favicon.ico";
            using var stream = asm.GetManifestResourceStream(embedded)
                ?? throw new InvalidOperationException($"Embedded favicon not found: {embedded}");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            iconBytes = ms.ToArray();
            contentType = "image/x-icon";
        }

        var headers = new HeaderDictionary
        {
            ["Content-Type"] = contentType,
            ["Cache-Control"] = "public,max-age=31536000"
        };
        if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            Log.Debug("Favicon content type: {ContentType}, size={Size} bytes", contentType, iconBytes.Length);
        }

        return app.Map("/favicon.ico", branch =>
        {
            branch.Run(async ctx =>
            {
                if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                {
                    Log.Debug("Serving favicon.ico, size={Size} bytes", iconBytes.Length);
                }

                ctx.Response.StatusCode = 200;
                foreach (var kv in headers)
                {
                    ctx.Response.Headers[kv.Key] = kv.Value;
                }

                await ctx.Response.Body.WriteAsync(iconBytes);
            });
        });
    }
}
