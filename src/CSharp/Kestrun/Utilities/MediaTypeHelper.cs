namespace Kestrun.Utilities;

internal static class MediaTypeHelper
{
    private static readonly Dictionary<string, string> ExactMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Canonical types
            ["application/json"] = "application/json",
            ["application/xml"] = "application/xml",
            ["application/yaml"] = "application/yaml",
            ["application/cbor"] = "application/cbor",

            // Common aliases
            ["application/x-yaml"] = "application/yaml",
            ["text/yaml"] = "application/yaml",
            ["text/xml"] = "application/xml",

            // Other supported
            ["application/bson"] = "application/bson",
            ["text/csv"] = "text/csv",
            ["application/x-www-form-urlencoded"] = "application/x-www-form-urlencoded",

            // Wildcards (treat as "fine, give me the default")
            ["*/*"] = "text/plain",
            ["text/*"] = "text/plain",
            ["application/*"] = "text/plain",
        };

    private static readonly (string Suffix, string Canonical)[] StructuredSuffixMap =
    [
        ("+json", "application/json"),
        ("+xml",  "application/xml"),
        ("+yaml", "application/yaml"),
        ("+yml",  "application/yaml"),
        ("+cbor", "application/cbor"),
    ];

    /// <summary>
    /// Normalizes the given content type by trimming whitespace and removing parameters.
    /// </summary>
    /// <param name="contentType">The content type to normalize.</param>
    /// <returns>The normalized media type.</returns>
    public static string Normalize(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        var semicolon = contentType.IndexOf(';');
        var mediaType = semicolon >= 0 ? contentType[..semicolon] : contentType;

        return mediaType.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Canonicalizes the given content type to a known media type.
    /// </summary>
    /// <param name="contentType">The content type to canonicalize.</param>
    /// <returns>The canonical media type.</returns>
    public static string Canonicalize(string? contentType)
    {
        var normalized = Normalize(contentType);
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        if (ExactMap.TryGetValue(normalized, out var exact))
        {
            return exact;
        }

        foreach (var (suffix, canonical) in StructuredSuffixMap)
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return canonical;
            }
        }

        return normalized;
    }
}
