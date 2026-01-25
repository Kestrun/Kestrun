using Microsoft.AspNetCore.RequestDecompression;
using Microsoft.Net.Http.Headers;
using Serilog.Events;

namespace Kestrun.Hosting.Compression;

/// <summary>
/// Extension methods for request decompression middleware.
/// </summary>
public static class KrRequestDecompressionExtensions
{
    /// <summary>
    /// Adds request decompression middleware with optional configuration.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="configure">Optional configuration delegate.</param>
    /// <returns>The configured host.</returns>
    public static KestrunHost AddRequestDecompression(this KestrunHost host, Action<RequestDecompressionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Adding request decompression (custom config: {HasConfig})", configure != null);
        }

        _ = host.AddService(services =>
        {
            _ = configure == null ? services.AddRequestDecompression() : services.AddRequestDecompression(configure);
        });

        return host.Use(app => app.UseRequestDecompression());
    }

    /// <summary>
    /// Adds request decompression middleware using allowed encodings.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="allowedEncodings">The allowed encodings (gzip, deflate, br).</param>
    /// <returns>The configured host.</returns>
    public static KestrunHost AddRequestDecompression(this KestrunHost host, IEnumerable<string>? allowedEncodings)
    {
        if (allowedEncodings == null)
        {
            return host.AddRequestDecompression();
        }

        var encodingSet = new HashSet<string>(allowedEncodings, StringComparer.OrdinalIgnoreCase);

        _ = host.Use(app => app.Use(async (ctx, next) =>
        {
            var encHeader = ctx.Request.Headers[HeaderNames.ContentEncoding].ToString();
            if (!string.IsNullOrWhiteSpace(encHeader))
            {
                var encodings = encHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var encoding in encodings)
                {
                    if (encoding.Equals("identity", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!encodingSet.Contains(encoding))
                    {
                        host.Logger.Warning("Rejected request Content-Encoding: {Encoding}", encoding);
                        ctx.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                        await ctx.Response.WriteAsync("Unsupported Content-Encoding.", ctx.RequestAborted).ConfigureAwait(false);
                        return;
                    }
                }
            }

            await next().ConfigureAwait(false);
        }));

        return host.AddRequestDecompression();
    }
}
