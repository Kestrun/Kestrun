using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RequestDecompression;
using Microsoft.Extensions.DependencyInjection;

namespace Kestrun.Forms;

/// <summary>
/// Extension methods for adding request decompression middleware to Kestrun.
/// </summary>
public static class KrRequestDecompressionExtensions
{
    /// <summary>
    /// Adds request decompression services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional configuration for request decompression options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKestrunRequestDecompression(
        this IServiceCollection services,
        Action<RequestDecompressionOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Add ASP.NET Core request decompression
        var builder = services.AddRequestDecompression(options =>
        {
            // Default providers: gzip, deflate, brotli
            // ASP.NET Core 7.0+ has built-in support for these
            configureOptions?.Invoke(options);
        });

        return services;
    }

    /// <summary>
    /// Uses request decompression middleware in the application pipeline.
    /// Must be called before endpoints that need to parse compressed request bodies.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseKestrunRequestDecompression(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Use ASP.NET Core request decompression middleware
        return app.UseRequestDecompression();
    }
}
