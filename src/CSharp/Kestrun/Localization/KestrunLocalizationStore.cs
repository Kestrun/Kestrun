using System.Collections.Concurrent;
using System.Globalization;

namespace Kestrun.Localization;

/// <summary>
/// Provides cached access to localized string tables with culture fallback support.
/// </summary>
public sealed class KestrunLocalizationStore
{
    private static readonly IReadOnlyDictionary<string, string> EmptyStrings =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly KestrunLocalizationOptions _options;
    private readonly ILogger _logger;
    private readonly string _resourcesRoot;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _cache;
    private readonly HashSet<string> _availableCultures;
    private readonly HashSet<string>? _supportedCultures;
    private readonly string _defaultCulture;

    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunLocalizationStore"/> class.
    /// </summary>
    /// <param name="options">The localization options.</param>
    /// <param name="contentRootPath">The content root path used to resolve resources.</param>
    /// <param name="logger">The logger instance.</param>
    public KestrunLocalizationStore(
        KestrunLocalizationOptions options,
        string contentRootPath,
        ILogger<KestrunLocalizationStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contentRootPath);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _logger = logger;
        _cache = new ConcurrentDictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        _availableCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _defaultCulture = NormalizeCulture(options.DefaultCulture) ?? options.DefaultCulture;

        _supportedCultures = options.SupportedCultures is { Length: > 0 }
            ? new HashSet<string>(options.SupportedCultures.Where(static c => !string.IsNullOrWhiteSpace(c)), StringComparer.OrdinalIgnoreCase)
            : null;

        if (_supportedCultures is not null && !_supportedCultures.Contains(_defaultCulture))
        {
            _logger.LogWarning(
                "Default culture '{DefaultCulture}' is not in SupportedCultures. Adding it for fallback resolution.",
                _defaultCulture);
            _ = _supportedCultures.Add(_defaultCulture);
        }

        _resourcesRoot = Path.IsPathRooted(_options.ResourcesBasePath)
            ? _options.ResourcesBasePath
            : Path.Combine(contentRootPath, _options.ResourcesBasePath);

        DiscoverAvailableCultures();
    }

    /// <summary>
    /// Resolves the requested culture to the closest available culture or the default culture.
    /// </summary>
    /// <param name="requestedCulture">The requested culture name.</param>
    /// <returns>The resolved culture name.</returns>
    public string ResolveCulture(string? requestedCulture) =>
        ResolveCulture(requestedCulture, allowDefaultFallback: true) ?? _defaultCulture;

    /// <summary>
    /// Gets the localized strings for the requested culture with fallback resolution.
    /// </summary>
    /// <param name="requestedCulture">The requested culture name.</param>
    /// <returns>A dictionary of localized strings.</returns>
    public IReadOnlyDictionary<string, string> GetStringsForCulture(string? requestedCulture)
    {
        var resolved = ResolveCulture(requestedCulture);
        return GetStringsForResolvedCulture(resolved);
    }

    /// <summary>
    /// Gets the localized strings for the already-resolved culture.
    /// </summary>
    /// <param name="resolvedCulture">The resolved culture name.</param>
    /// <returns>A dictionary of localized strings.</returns>
    public IReadOnlyDictionary<string, string> GetStringsForResolvedCulture(string resolvedCulture)
    {
        return string.IsNullOrWhiteSpace(resolvedCulture)
            ? EmptyStrings
            : _cache.GetOrAdd(resolvedCulture, LoadStringsForCulture);
    }

    /// <summary>
    /// Gets the localized strings for the specified culture using fallback resolution.
    /// </summary>
    /// <param name="culture">The culture name to resolve.</param>
    public IReadOnlyDictionary<string, string> this[string culture] => GetStringsForCulture(culture);

    internal string? ResolveCulture(string? requestedCulture, bool allowDefaultFallback)
    {
        var candidates = BuildCultureCandidates(requestedCulture, includeDefault: allowDefaultFallback);

        foreach (var candidate in candidates)
        {
            if (!IsCultureAllowed(candidate))
            {
                continue;
            }

            if (IsCultureAvailable(candidate))
            {
                return candidate;
            }
        }

        return allowDefaultFallback && IsCultureAllowed(_defaultCulture)
            ? _defaultCulture
            : null;
    }

    private IReadOnlyList<string> BuildCultureCandidates(string? requestedCulture, bool includeDefault)
    {
        var list = new List<string>();
        var normalized = NormalizeCulture(requestedCulture);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            var culture = CultureInfo.GetCultureInfo(normalized);
            while (!string.IsNullOrWhiteSpace(culture.Name))
            {
                list.Add(culture.Name);
                culture = culture.Parent;
            }
        }

        if (includeDefault && IsCultureAllowed(_defaultCulture))
        {
            list.Add(_defaultCulture);
        }

        return list;
    }

    private void DiscoverAvailableCultures()
    {
        if (!Directory.Exists(_resourcesRoot))
        {
            _logger.LogDebug("Localization base path '{BasePath}' does not exist.", _resourcesRoot);
            return;
        }

        foreach (var dir in Directory.GetDirectories(_resourcesRoot))
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var filePath = Path.Combine(dir, _options.FileName);
            if (File.Exists(filePath))
            {
                _ = _availableCultures.Add(name);
            }
        }

        _logger.LogDebug("Discovered {Count} localization cultures in {BasePath}.", _availableCultures.Count, _resourcesRoot);
    }

    private bool IsCultureAvailable(string culture) => _availableCultures.Contains(culture);

    private bool IsCultureAllowed(string culture) => _supportedCultures == null || _supportedCultures.Contains(culture);

    private IReadOnlyDictionary<string, string> LoadStringsForCulture(string culture)
    {
        var filePath = Path.Combine(_resourcesRoot, culture, _options.FileName);
        if (!File.Exists(filePath))
        {
            _logger.LogWarning(
                "Localization file missing for culture '{Culture}' at '{Path}'.",
                culture,
                filePath);
            return EmptyStrings;
        }

        try
        {
            return StringTableParser.ParseFile(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse localization file '{Path}'.", filePath);
            return EmptyStrings;
        }
    }

    private static string? NormalizeCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo(culture).Name;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
