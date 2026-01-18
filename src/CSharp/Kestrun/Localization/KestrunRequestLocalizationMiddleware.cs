using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Serilog;
using SerilogILogger = Serilog.ILogger;

namespace Kestrun.Localization;

/// <summary>
/// Kestrun-specific request localization middleware that determines culture once per request
/// and stores it in HttpContext.Items for PowerShell handlers to access.
/// </summary>
public class KestrunRequestLocalizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RequestLocalizationOptions _options;
    private readonly SerilogILogger _logger;

    /// <summary>
    /// The key used to store the culture in HttpContext.Items.
    /// </summary>
    public const string CultureItemKey = "KrCulture";

    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunRequestLocalizationMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="options">The request localization options.</param>
    /// <param name="logger">The Serilog logger instance.</param>
    public KestrunRequestLocalizationMiddleware(
        RequestDelegate next,
        RequestLocalizationOptions options,
        SerilogILogger logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes the HTTP request and determines the culture.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // Determine the request culture using the configured providers
        RequestCulture? requestCulture = null;
        IRequestCultureProvider? winningProvider = null;

        foreach (var provider in _options.RequestCultureProviders)
        {
            var providerResult = await provider.DetermineProviderCultureResult(context);
            if (providerResult != null)
            {
                var cultures = providerResult.Cultures;
                var uiCultures = providerResult.UICultures;

                CultureInfo? culture = null;
                CultureInfo? uiCulture = null;

                if (cultures.Count > 0 && !string.IsNullOrEmpty(cultures[0].Value))
                {
                    culture = GetCultureInfo(cultures[0].Value!, _options.SupportedCultures, _options.FallBackToParentCultures);
                }

                if (uiCultures.Count > 0 && !string.IsNullOrEmpty(uiCultures[0].Value))
                {
                    uiCulture = GetCultureInfo(uiCultures[0].Value!, _options.SupportedUICultures, _options.FallBackToParentUICultures);
                }

                if (culture != null || uiCulture != null)
                {
                    culture ??= _options.DefaultRequestCulture.Culture;
                    uiCulture ??= _options.DefaultRequestCulture.UICulture;

                    requestCulture = new RequestCulture(culture, uiCulture);
                    winningProvider = provider;
                    break;
                }
            }
        }

        // Fall back to default if no provider succeeded
        requestCulture ??= _options.DefaultRequestCulture;

        // Store the culture in HttpContext.Items for PowerShell handlers
        context.Items[CultureItemKey] = requestCulture.UICulture.Name;

        // Set the current thread's culture for formatting
        CultureInfo.CurrentCulture = requestCulture.Culture;
        CultureInfo.CurrentUICulture = requestCulture.UICulture;

        if (_logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            _logger.Debug(
                "Request culture determined: Culture={Culture}, UICulture={UICulture}, Provider={Provider}",
                requestCulture.Culture.Name,
                requestCulture.UICulture.Name,
                winningProvider?.GetType().Name ?? "Default");
        }

        // Apply culture to response headers if configured
        if (_options.ApplyCurrentCultureToResponseHeaders)
        {
            context.Response.Headers.ContentLanguage = requestCulture.UICulture.Name;
        }

        await _next(context);
    }

    /// <summary>
    /// Gets a CultureInfo from the culture string, with optional fallback to parent cultures.
    /// </summary>
    private static CultureInfo? GetCultureInfo(string culture, IList<CultureInfo>? supportedCultures, bool fallbackToParent)
    {
        if (supportedCultures == null || supportedCultures.Count == 0)
        {
            return null;
        }

        try
        {
            var cultureInfo = new CultureInfo(culture);

            // Check if exact match is supported
            if (supportedCultures.Any(c => c.Name.Equals(cultureInfo.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return cultureInfo;
            }

            // Try parent culture fallback if enabled
            if (fallbackToParent && !string.IsNullOrEmpty(cultureInfo.Parent.Name))
            {
                var parentCulture = cultureInfo.Parent;
                if (supportedCultures.Any(c => c.Name.Equals(parentCulture.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return parentCulture;
                }
            }
        }
        catch (CultureNotFoundException)
        {
            // Invalid culture, ignore
        }

        return null;
    }
}
