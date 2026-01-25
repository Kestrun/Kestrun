using Microsoft.AspNetCore.Http;

namespace Kestrun.Forms;

/// <summary>
/// Options for configuring form parsing behavior.
/// </summary>
public sealed record KrFormOptions
{
    /// <summary>
    /// Gets the allowed request content types (supports wildcards like multipart/*).
    /// Default includes multipart/form-data, application/x-www-form-urlencoded, multipart/mixed, multipart/related, multipart/byteranges.
    /// </summary>
    public List<string> AllowedRequestContentTypes { get; init; } = new()
    {
        "multipart/form-data",
        "application/x-www-form-urlencoded",
        "multipart/mixed",
        "multipart/related",
        "multipart/byteranges"
    };

    /// <summary>
    /// Gets a value indicating whether to reject requests with unknown content types.
    /// </summary>
    public bool RejectUnknownRequestContentType { get; init; } = true;

    /// <summary>
    /// Gets the maximum request body size in bytes.
    /// </summary>
    public long? MaxRequestBodyBytes { get; init; } = 100 * 1024 * 1024; // 100 MB default

    /// <summary>
    /// Gets the maximum body size per part in bytes.
    /// </summary>
    public long MaxPartBodyBytes { get; init; } = 10 * 1024 * 1024; // 10 MB default

    /// <summary>
    /// Gets the maximum number of parts allowed.
    /// </summary>
    public int MaxParts { get; init; } = 100;

    /// <summary>
    /// Gets the maximum header bytes per part.
    /// </summary>
    public long MaxHeaderBytesPerPart { get; init; } = 16 * 1024; // 16 KB default

    /// <summary>
    /// Gets the maximum field value bytes for text fields.
    /// </summary>
    public long MaxFieldValueBytes { get; init; } = 1024 * 1024; // 1 MB default

    /// <summary>
    /// Gets the maximum nesting depth for multipart sections.
    /// </summary>
    public int MaxNestingDepth { get; init; } = 1;

    /// <summary>
    /// Gets the default upload path for storing temporary files.
    /// </summary>
    public string DefaultUploadPath { get; init; } = Path.GetTempPath();

    /// <summary>
    /// Gets the filename sanitizer function.
    /// Default uses Path.GetFileName with fallback to random name.
    /// </summary>
    public Func<string?, string> SanitizeFileName { get; init; } = DefaultSanitizeFileName;

    /// <summary>
    /// Gets a value indicating whether to compute SHA-256 hashes for parts.
    /// </summary>
    public bool ComputeSha256 { get; init; }

    /// <summary>
    /// Gets a value indicating whether part-level decompression is enabled.
    /// </summary>
    public bool EnablePartDecompression { get; init; }

    /// <summary>
    /// Gets the allowed part content encodings (identity, gzip, deflate, br).
    /// </summary>
    public List<string> AllowedPartContentEncodings { get; init; } = new()
    {
        "identity",
        "gzip",
        "deflate",
        "br"
    };

    /// <summary>
    /// Gets the maximum decompressed bytes per part (for decompression bomb protection).
    /// </summary>
    public long MaxDecompressedBytesPerPart { get; init; } = 100 * 1024 * 1024; // 100 MB default

    /// <summary>
    /// Gets a value indicating whether to reject parts with unknown content encodings.
    /// </summary>
    public bool RejectUnknownContentEncoding { get; init; } = true;

    /// <summary>
    /// Gets the list of part-specific rules.
    /// </summary>
    public List<KrPartRule> Rules { get; init; } = new();

    /// <summary>
    /// Gets the hook for processing individual parts.
    /// Return Skip to skip the part, Reject to reject the request, Continue to process normally.
    /// </summary>
    public Func<KrPartContext, ValueTask<KrPartAction>>? OnPart { get; init; }

    /// <summary>
    /// Gets the hook called after parsing is completed.
    /// Return non-null to short-circuit and send that object as response.
    /// </summary>
    public Func<KrFormContext, ValueTask<object?>>? OnCompleted { get; init; }

    /// <summary>
    /// Default filename sanitizer that uses Path.GetFileName with fallback to random name.
    /// </summary>
    private static string DefaultSanitizeFileName(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return $"upload_{Guid.NewGuid():N}";
        }

        try
        {
            var sanitized = Path.GetFileName(filename);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                return $"upload_{Guid.NewGuid():N}";
            }
            return sanitized;
        }
        catch
        {
            return $"upload_{Guid.NewGuid():N}";
        }
    }
}
