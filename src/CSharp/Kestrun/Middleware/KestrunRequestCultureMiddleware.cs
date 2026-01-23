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

        ApplyCulture(contextCulture);

        await _next(context);
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

    private bool TryGetAcceptLanguageCulture(IHeaderDictionary headers, out string? culture)
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

        // Prefer the first language token as the requested culture (do not resolve here).
        foreach (var token in header.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = token.Trim();
            if (segment.Length == 0)
            {
                continue;
            }

            var separatorIndex = segment.IndexOf(';');
            var candidate = separatorIndex >= 0 ? segment[..separatorIndex].Trim() : segment;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            culture = candidate;
            return true;
        }

        culture = null;
        return false;
    }

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
