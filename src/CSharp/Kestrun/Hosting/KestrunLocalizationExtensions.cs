using Kestrun.Localization;
using System;
using Kestrun.Middleware;

namespace Kestrun.Hosting;

/// <summary>
/// Extension methods for configuring Kestrun localization on a <see cref="KestrunHost"/>.
/// </summary>
public static class KestrunLocalizationExtensions
{
    /// <summary>
    /// Adds localization middleware using the specified options.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="configure">Optional configuration for localization options.</param>
    /// <returns>The configured host.</returns>
    public static KestrunHost AddLocalization(
        this KestrunHost host,
        Action<KestrunLocalizationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        var options = new KestrunLocalizationOptions();
        configure?.Invoke(options);

        return host.Use(app => app.UseKestrunLocalization(options));
    }

    /// <summary>
    /// Adds localization middleware using the specified options instance.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="options">The localization options.</param>
    /// <returns>The configured host.</returns>
    public static KestrunHost AddLocalization(
        this KestrunHost host,
        KestrunLocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(options);

        return host.Use(app => app.UseKestrunLocalization(options));
    }
}
