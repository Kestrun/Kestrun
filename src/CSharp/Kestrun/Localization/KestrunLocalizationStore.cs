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

    private readonly KestrunLocalizationOptions options;
    private readonly Serilog.ILogger logger;
    private readonly string resourcesRoot;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> cache;
    private readonly HashSet<string> availableCultures;
    private readonly string defaultCulture;

    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunLocalizationStore"/> class.
    /// </summary>
    /// <param name="options">The localization options.</param>
    /// <param name="contentRootPath">The content root path used to resolve resources.</param>
    /// <param name="logger">The logger instance.</param>
    public KestrunLocalizationStore(
        KestrunLocalizationOptions options,
        string contentRootPath,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contentRootPath);
        ArgumentNullException.ThrowIfNull(logger);

        this.options = options;
        this.logger = logger;
        cache = new ConcurrentDictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        availableCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        defaultCulture = NormalizeCulture(options.DefaultCulture) ?? options.DefaultCulture;

        resourcesRoot = Path.IsPathRooted(this.options.ResourcesBasePath)
            ? this.options.ResourcesBasePath
            : Path.Combine(contentRootPath, this.options.ResourcesBasePath);

        DiscoverAvailableCultures();
    }

    /// <summary>
    /// Gets the set of available cultures discovered at startup (read-only).
    /// </summary>
    public IReadOnlyCollection<string> AvailableCultures => [.. availableCultures];

    /// <summary>
    /// Resolves the requested culture to the closest available culture or the default culture.
    /// </summary>
    /// <param name="requestedCulture">The requested culture name.</param>
    /// <returns>The resolved culture name.</returns>
    public string ResolveCulture(string? requestedCulture) =>
        ResolveCulture(requestedCulture, allowDefaultFallback: true) ?? defaultCulture;

    /// <summary>
    /// Gets the localized strings for the requested culture with fallback resolution.
    /// </summary>
    /// <param name="requestedCulture">The requested culture name.</param>
    /// <returns>A dictionary of localized strings.</returns>
    public IReadOnlyDictionary<string, string> GetStringsForCulture(string? requestedCulture) => new CompositeStringTable(this, requestedCulture);

    /// <summary>
    /// Gets the localized strings for the already-resolved culture.
    /// </summary>
    /// <param name="resolvedCulture">The resolved culture name.</param>
    /// <returns>A dictionary of localized strings.</returns>
    public IReadOnlyDictionary<string, string> GetStringsForResolvedCulture(string resolvedCulture)
    {
        return string.IsNullOrWhiteSpace(resolvedCulture)
            ? EmptyStrings
            : (IsCultureAvailable(resolvedCulture) ||
               string.Equals(resolvedCulture, defaultCulture, StringComparison.OrdinalIgnoreCase)
                ? cache.GetOrAdd(resolvedCulture, LoadStringsForCulture)
                : EmptyStrings);
    }

    private sealed class CompositeStringTable : IReadOnlyDictionary<string, string>
    {
        private readonly KestrunLocalizationStore _store;
        private readonly List<string> _candidates;

        public CompositeStringTable(KestrunLocalizationStore store, string? requestedCulture)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _candidates = [];

            // Build candidate list from requested culture up the parent chain
            var baseCandidates = _store.BuildCultureCandidates(requestedCulture);
            if (baseCandidates.Count > 0)
            {
                _candidates.Add(baseCandidates[0]);
            }

            // Add the specific-culture sibling fallback before walking the parent chain
            var specific = GetSpecificCultureFallback(requestedCulture);
            if (!string.IsNullOrWhiteSpace(specific) && !_candidates.Contains(specific, StringComparer.OrdinalIgnoreCase))
            {
                _candidates.Add(specific);
            }

            for (var i = 1; i < baseCandidates.Count; i++)
            {
                var candidate = baseCandidates[i];
                if (!_candidates.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                {
                    _candidates.Add(candidate);
                }
            }

            // Finally add the default culture as last resort (if not already present)
            if (!string.IsNullOrWhiteSpace(_store.defaultCulture) && !_candidates.Contains(_store.defaultCulture, StringComparer.OrdinalIgnoreCase))
            {
                _candidates.Add(_store.defaultCulture);
            }
        }

        public string this[string key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);

        public IEnumerable<string> Keys
        {
            get
            {
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var candidate in _candidates)
                {
                    if (!IsUsableCandidate(candidate))
                    {
                        continue;
                    }

                    var dict = _store.cache.GetOrAdd(candidate, _store.LoadStringsForCulture);
                    foreach (var k in dict.Keys)
                    {
                        if (set.Add(k))
                        {
                            yield return k;
                        }
                    }
                }
            }
        }

        public IEnumerable<string> Values
        {
            get
            {
                foreach (var key in Keys)
                {
                    yield return this[key];
                }
            }
        }

        public int Count => Keys.Count();

        public bool ContainsKey(string key) => TryGetValue(key, out _);

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (var key in Keys)
            {
                yield return new KeyValuePair<string, string>(key, this[key]);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Tries to get the localized value for the specified key using culture fallback.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">The localized value if found; otherwise, an empty string.</param>
        /// <returns>True if the key was found; otherwise, false.</returns>
        public bool TryGetValue(string key, out string value)
        {
            foreach (var candidate in _candidates)
            {
                if (!IsUsableCandidate(candidate))
                {
                    continue;
                }

                var dict = _store.cache.GetOrAdd(candidate, _store.LoadStringsForCulture);

                if (dict.TryGetValue(key, out var v))
                {
                    value = v;
                    return true;
                }
            }

            value = string.Empty; // or throw / fallback marker
            return false;
        }

        private bool IsUsableCandidate(string candidate)
        {
            // Prevent unbounded cache growth from arbitrary culture tags.
            // Only cache/load cultures that were discovered as available, plus the configured default.
            return _store.IsCultureAvailable(candidate) ||
                   string.Equals(candidate, _store.defaultCulture, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Gets the localized strings for the specified culture using fallback resolution.
    /// </summary>
    /// <param name="culture">The culture name to resolve.</param>
    public IReadOnlyDictionary<string, string> this[string culture] => GetStringsForCulture(culture);

    internal string? ResolveCulture(string? requestedCulture, bool allowDefaultFallback)
    {
        var candidates = BuildCultureCandidates(requestedCulture);

        // Prefer an exact match for the requested culture first (typically the first candidate).
        if (candidates.Count > 0 && IsCultureAvailable(candidates[0]))
        {
            return candidates[0];
        }

        // Then attempt a sibling-specific fallback (e.g., fr-FR when resolving fr-CA) before walking parents.
        var specificFallback = GetSpecificCultureFallback(requestedCulture);
        if (specificFallback is not null && IsCultureAvailable(specificFallback))
        {
            return specificFallback;
        }

        // Finally, walk the remaining candidates (typically the parent chain).
        for (var i = 1; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (IsCultureAvailable(candidate))
            {
                return candidate;
            }
        }

        return allowDefaultFallback
            ? defaultCulture
            : null;
    }

    private IReadOnlyList<string> BuildCultureCandidates(string? requestedCulture)
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

        return list;
    }

    private static string? GetSpecificCultureFallback(string? requestedCulture)
    {
        if (string.IsNullOrWhiteSpace(requestedCulture))
        {
            return null;
        }

        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(requestedCulture);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }

        var neutral = culture.IsNeutralCulture ? culture : culture.Parent;
        if (neutral is null || string.IsNullOrWhiteSpace(neutral.Name))
        {
            return null;
        }

        try
        {
            var specific = CultureInfo.CreateSpecificCulture(neutral.Name).Name;
            return string.Equals(specific, culture.Name, StringComparison.OrdinalIgnoreCase)
                ? null
                : specific;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private void DiscoverAvailableCultures()
    {
        if (!Directory.Exists(resourcesRoot))
        {
            if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                logger.Debug("Localization resources root '{BasePath}' does not exist; no cultures discovered.", resourcesRoot);
            }
            return;
        }

        foreach (var dir in Directory.GetDirectories(resourcesRoot))
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var filePath = Path.Combine(dir, options.FileName);
            if (File.Exists(filePath) || HasJsonFallback(filePath))
            {
                _ = availableCultures.Add(name);
            }
        }

        if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            logger.Debug("Discovered {Count} localization cultures in {BasePath}.", availableCultures.Count, resourcesRoot);
        }
    }

    private bool IsCultureAvailable(string culture) => availableCultures.Contains(culture);

    private static bool HasJsonFallback(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (!string.Equals(extension, ".psd1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var jsonPath = Path.ChangeExtension(filePath, ".json");
        return File.Exists(jsonPath);
    }

    private IReadOnlyDictionary<string, string> LoadStringsForCulture(string culture)
    {
        var filePath = Path.Combine(resourcesRoot, culture, options.FileName);
        var extension = Path.GetExtension(filePath);
        var primaryPath = filePath;
        var jsonFallbackPath = string.Equals(extension, ".psd1", StringComparison.OrdinalIgnoreCase)
            ? Path.ChangeExtension(filePath, ".json")
            : null;

        if (!File.Exists(primaryPath))
        {
            if (!string.IsNullOrWhiteSpace(jsonFallbackPath) && File.Exists(jsonFallbackPath))
            {
                return LoadJsonStrings(jsonFallbackPath);
            }
            if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                logger.Debug(
                    "Localization file missing for culture '{Culture}'. Tried '{PrimaryPath}'{JsonPathMessage}.",
                    culture,
                    primaryPath,
                    jsonFallbackPath is null ? string.Empty : $" and '{jsonFallbackPath}'");
            }
            return EmptyStrings;
        }

        return string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
            ? LoadJsonStrings(primaryPath)
            : LoadPsStringTable(primaryPath);
    }

    private IReadOnlyDictionary<string, string> LoadPsStringTable(string path)
    {
        try
        {
            return StringTableParser.ParseFile(path);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to parse localization file '{Path}'.", path);
            return EmptyStrings;
        }
    }

    private IReadOnlyDictionary<string, string> LoadJsonStrings(string path)
    {
        try
        {
            return StringTableParser.ParseJsonFile(path);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to parse localization JSON file '{Path}'.", path);
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
