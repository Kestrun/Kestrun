using System.Globalization;
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

        KestrunHost? host = null;
        try
        {
            host = services.GetService<KestrunHost>();
        }
        catch
        {
            // Ignore any errors when host isn't available in this context.
        }

        var logger = host?.Logger ?? Serilog.Log.Logger;
        var store = new KestrunLocalizationStore(options, contentRoot, logger);

        // Set the default thread culture if specified in options.
        if (options.SetDefaultThreadCulture && !string.IsNullOrWhiteSpace(options.DefaultCulture))
        {
            try
            {
                var ci = CultureInfo.GetCultureInfo(options.DefaultCulture);
                CultureInfo.DefaultThreadCurrentCulture = ci;
                CultureInfo.DefaultThreadCurrentUICulture = ci;
                logger.Information("Default thread culture set to {Culture}", ci.Name);
            }
            catch (CultureNotFoundException)
            {
                logger.Warning("The specified default culture '{Culture}' is not valid.", options.DefaultCulture);
            }
        }

        // If a KestrunHost is available via DI, capture the store on the host so tools
        // and PowerShell helpers can inspect runtime-loaded cultures.
        _ = (host?.LocalizationStore = store);
        return app.UseMiddleware<KestrunRequestCultureMiddleware>(store, options);
    }
}
