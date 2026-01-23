using Kestrun.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kestrun.Middleware;

/// <summary>
/// Extension methods for adding Kestrun localization middleware.
/// </summary>
public static class KestrunLocalizationMiddlewareExtensions
{
    /// <summary>
    /// Adds Kestrun localization middleware to the pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="options">The localization options.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseKestrunLocalization(
        this IApplicationBuilder app,
        KestrunLocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);

        var services = app.ApplicationServices;
        var env = services.GetService<IHostEnvironment>();
        var contentRoot = env?.ContentRootPath ?? Directory.GetCurrentDirectory();

        var loggerFactory = services.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger<KestrunLocalizationStore>() ?? NullLogger<KestrunLocalizationStore>.Instance;

        var store = new KestrunLocalizationStore(options, contentRoot, logger);
        return app.UseMiddleware<KestrunRequestCultureMiddleware>(store, options);
    }
}
