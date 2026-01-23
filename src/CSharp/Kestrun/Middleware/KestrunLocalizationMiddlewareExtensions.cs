using Kestrun.Hosting;
using Kestrun.Localization;

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

        var store = new KestrunLocalizationStore(options, contentRoot, Serilog.Log.Logger);
        // If a KestrunHost is available via DI, capture the store on the host so tools
        // and PowerShell helpers can inspect runtime-loaded cultures.
        try
        {
            var host = services.GetService<KestrunHost>();
            _ = (host?.LocalizationStore = store);
        }
        catch
        {
            // Ignore any errors when host isn't available in this context.
        }
        return app.UseMiddleware<KestrunRequestCultureMiddleware>(store, options);
    }
}
