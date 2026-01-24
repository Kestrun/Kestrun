using System.Globalization;
using Kestrun.Localization;

namespace Kestrun.Middleware;

/// <summary>
/// Middleware that resolves the request culture once and exposes localized strings.
/// </summary>
public sealed class KestrunRequestCultureMiddleware(
    RequestDelegate next,
    KestrunLocalizationStore store,
    KestrunLocalizationOptions options)
{
    private const string CultureItemKey = "KrCulture";
    private const string StringsItemKey = "KrStrings";
    private const string LocalizerItemKey = "KrLocalizer";

    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly KestrunLocalizationStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly KestrunLocalizationOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Invokes the middleware for the specified request context.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that represents the completion of request processing.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var requestedCulture = NormalizeCultureName(ResolveRequestedCulture(context));

        // Resolve which resource culture to use for string table lookup (may be a fallback
        // such as using 'it-IT' resources for a requested 'it-CH'). Keep the original
        // requested culture so formatting (dates/currency) uses the exact requested culture.
        // Obtain a strings table that performs per-key fallback across candidate cultures
        // based on the original requested culture. This ensures that missing keys in a
        // specific culture (e.g., fr-CA) can fall back to a related resource (e.g., fr-FR)
        // while preserving the requested culture for formatting.
        var strings = _store.GetStringsForCulture(requestedCulture);

        // Resolve which resource culture was chosen for logging/diagnostics; this may be
        // different from the requested culture (it's the resolved culture folder).
        var resourceCulture = _store.ResolveCulture(requestedCulture);

        // Expose the request culture (preferred) to the pipeline; if none was requested,
        // fall back to the resolved resource culture or the configured default.
        var contextCulture = !string.IsNullOrWhiteSpace(requestedCulture)
            ? requestedCulture
            : (!string.IsNullOrWhiteSpace(resourceCulture) ? resourceCulture : _options.DefaultCulture);

        context.Items[CultureItemKey] = contextCulture;
        context.Items[StringsItemKey] = strings;
        context.Items[LocalizerItemKey] = strings;

        // Preserve the original culture to restore after the request is complete.
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;

        try
        {
            // Set the current thread culture for formatting purposes.
            ApplyCulture(contextCulture);

            await _next(context);
        }
        finally
        {
            // Restore the original culture after the request is complete.
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    private string? ResolveRequestedCulture(HttpContext context)
    {
        return _options.EnableQuery && TryGetQueryCulture(context.Request.Query, out var queryCulture)
            ? queryCulture
            : _options.EnableCookie && TryGetCookieCulture(context.Request.Cookies, out var cookieCulture)
                ? cookieCulture
                : _options.EnableAcceptLanguage && TryGetAcceptLanguageCulture(context.Request.Headers, out var headerCulture)
                    ? headerCulture
                    : null;
    }

    private bool TryGetQueryCulture(IQueryCollection query, out string? culture)
    {
        if (query.TryGetValue(_options.QueryKey, out var values))
        {
            var candidate = values.ToString();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                culture = candidate;
                return true;
            }
        }

        culture = null;
        return false;
    }

    private bool TryGetCookieCulture(IRequestCookieCollection cookies, out string? culture)
    {
        if (cookies.TryGetValue(_options.CookieName, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            culture = value;
            return true;
        }

        culture = null;
        return false;
    }

    /// <summary>
    /// Tries to resolve the culture from the Accept-Language header.
    /// </summary>
    /// <param name="headers">The request headers.</param>
    /// <param name="culture">The resolved culture, if any.</param>
    /// <returns>True if a culture was resolved; otherwise, false.</returns>
    private static bool TryGetAcceptLanguageCulture(IHeaderDictionary headers, out string? culture)
    {
        if (!headers.TryGetValue("Accept-Language", out var values))
        {
            culture = null;
            return false;
        }

        var header = values.ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            culture = null;
            return false;
        }

        return TrySelectBestAcceptLanguageCandidate(header, out culture);
    }

    /// <summary>
    /// Selects the best Accept-Language candidate from a raw header value.
    /// </summary>
    /// <param name="header">The raw Accept-Language header string.</param>
    /// <param name="culture">The best candidate culture (language range), if any.</param>
    /// <returns>True if a candidate was selected; otherwise, false.</returns>
    private static bool TrySelectBestAcceptLanguageCandidate(string header, out string? culture)
    {
        // Select the highest-quality culture token as the requested culture (do not resolve here).
        // Example: "en-US;q=0.1, fr-FR;q=0.9" should prefer "fr-FR".
        string? bestCandidate = null;
        var bestQuality = -1.0;

        var tokens = header.Split(',', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!TryParseAcceptLanguageToken(tokens[i], out var candidate, out var quality))
            {
                continue;
            }

            // For ties, keep the first token in header order.
            if (quality > bestQuality)
            {
                bestQuality = quality;
                bestCandidate = candidate;
            }
        }

        if (!string.IsNullOrWhiteSpace(bestCandidate))
        {
            culture = bestCandidate;
            return true;
        }

        culture = null;
        return false;
    }

    /// <summary>
    /// Parses a single Accept-Language token (language-range plus optional parameters).
    /// </summary>
    /// <param name="token">A token from the Accept-Language header (e.g. "fr-FR;q=0.9").</param>
    /// <param name="candidate">The parsed language-range candidate.</param>
    /// <param name="quality">The parsed q-value (defaults to 1.0 if not specified).</param>
    /// <returns>True if the token produced a usable candidate; otherwise, false.</returns>
    private static bool TryParseAcceptLanguageToken(string token, out string candidate, out double quality)
    {
        candidate = string.Empty;
        quality = 1.0;

        var segment = token.Trim();
        if (segment.Length == 0)
        {
            return false;
        }

        var parts = segment.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var parsedCandidate = parts[0];
        if (string.IsNullOrWhiteSpace(parsedCandidate) || parsedCandidate == "*")
        {
            return false;
        }

        if (!TryParseQualityParameter(parts, out var parsedQuality))
        {
            return false;
        }

        candidate = parsedCandidate;
        quality = parsedQuality;
        return true;
    }

    /// <summary>
    /// Parses the q-value parameter from an Accept-Language token's parameters.
    /// </summary>
    /// <param name="parts">Token parts split by ';' where index 0 is the language-range.</param>
    /// <param name="quality">The parsed q-value, defaulting to 1.0.</param>
    /// <returns>True if the q-value is within [0,1]; otherwise, false.</returns>
    private static bool TryParseQualityParameter(string[] parts, out double quality)
    {
        quality = 1.0;

        for (var p = 1; p < parts.Length; p++)
        {
            var parameter = parts[p];
            if (!parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (double.TryParse(
                    parameter.AsSpan(2),
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var parsedQuality))
            {
                quality = parsedQuality;
            }

            break;
        }

        return quality is >= 0.0 and <= 1.0;
    }

    /// <summary>
    /// Applies the specified culture to the current thread.
    /// </summary>
    /// <param name="resolvedCulture">The culture name to apply.</param>
    private static void ApplyCulture(string resolvedCulture)
    {
        if (string.IsNullOrWhiteSpace(resolvedCulture))
        {
            return;
        }

        try
        {
            var cultureInfo = CultureInfo.GetCultureInfo(resolvedCulture);
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
        }
        catch (CultureNotFoundException)
        {
            // Ignore invalid cultures to avoid breaking the request pipeline.

        }
    }

    /// <summary>
    /// Normalizes and validates a culture name.
    /// </summary>
    /// <param name="culture">The culture name to normalize.</param>
    /// <returns>The normalized culture name, or null if invalid.</returns>
    private static string? NormalizeCultureName(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return null;
        }

        var candidate = culture.Trim().Replace('_', '-');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        try
        {
            var normalized = CultureInfo.GetCultureInfo(candidate).Name;

            // Treat inputs that normalize by truncation or otherwise changing the tag (beyond casing)
            // as invalid. Example: 'not-a-culture' normalizing to 'not'.
            return string.Equals(normalized, candidate, StringComparison.OrdinalIgnoreCase)
                ? normalized
                : null;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
