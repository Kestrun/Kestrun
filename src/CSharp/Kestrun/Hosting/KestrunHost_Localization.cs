using Kestrun.Localization;
using Kestrun.Middleware;

namespace Kestrun.Hosting;

/// <summary>
/// Extension methods for adding localization to the Kestrun host.
/// </summary>
public partial class KestrunHost
{
    /// <summary>
    /// Adds localization middleware using the specified options.
    /// </summary>
    /// <param name="configure">Optional configuration for localization options.</param>
    /// <returns>The configured host.</returns>
    public KestrunHost AddLocalization(
        Action<KestrunLocalizationOptions>? configure = null)
    {
        var options = new KestrunLocalizationOptions();
        configure?.Invoke(options);

        return Use(app => app.UseKestrunLocalization(options));
    }

    /// <summary>
    /// Adds localization middleware using the specified options instance.
    /// </summary>
    /// <param name="options">The localization options.</param>
    /// <returns>The configured host.</returns>
    public KestrunHost AddLocalization(KestrunLocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Use(app => app.UseKestrunLocalization(options));
    }
}
