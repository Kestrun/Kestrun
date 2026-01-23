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

        var requestedCulture = ResolveRequestedCulture(context);
        var resolvedCulture = _store.ResolveCulture(requestedCulture);
        var strings = _store.GetStringsForResolvedCulture(resolvedCulture);

        context.Items[CultureItemKey] = resolvedCulture;
        context.Items[StringsItemKey] = strings;
        context.Items[LocalizerItemKey] = _store;

        ApplyCulture(resolvedCulture);

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

            var resolved = _store.ResolveCulture(candidate, allowDefaultFallback: false);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                culture = resolved;
                return true;
            }
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
}
