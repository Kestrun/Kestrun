namespace Kestrun.Forms;

/// <summary>
/// Configures form parsing behavior for Kestrun.
/// </summary>
public sealed class KrFormOptions
{
    /// <summary>
    /// Gets the allowed request content types.
    /// </summary>
    public List<string> AllowedRequestContentTypes { get; } =
    [
        "multipart/form-data",
        "application/x-www-form-urlencoded",
        "multipart/mixed",
        "multipart/related",
        "multipart/byteranges"
    ];

    /// <summary>
    /// Gets or sets a value indicating whether unknown request content types should be rejected.
    /// </summary>
    public bool RejectUnknownRequestContentType { get; set; } = true;

    /// <summary>
    /// Gets the form parsing limits.
    /// </summary>
    public KrFormLimits Limits { get; } = new();

    /// <summary>
    /// Gets or sets the default upload path for stored parts.
    /// </summary>
    public string DefaultUploadPath { get; set; } = Path.Combine(Path.GetTempPath(), "kestrun-uploads");

    /// <summary>
    /// Gets or sets the filename sanitizer.
    /// </summary>
    public Func<string, string> SanitizeFileName { get; set; } = static name =>
    {
        var sanitized = Path.GetFileName(name);
        return string.IsNullOrWhiteSpace(sanitized) ? Path.GetRandomFileName() : sanitized;
    };

    /// <summary>
    /// Gets or sets a value indicating whether SHA-256 hashes should be computed for file parts.
    /// </summary>
    public bool ComputeSha256 { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether per-part decompression is enabled.
    /// </summary>
    public bool EnablePartDecompression { get; set; }

    /// <summary>
    /// Gets the allowed part content encodings for decompression.
    /// </summary>
    public List<string> AllowedPartContentEncodings { get; } =
    [
        "identity",
        "gzip",
        "deflate",
        "br"
    ];

    /// <summary>
    /// Gets or sets the maximum decompressed bytes per part.
    /// </summary>
    public long MaxDecompressedBytesPerPart { get; set; } = 20 * 1024 * 1024;

    /// <summary>
    /// Gets or sets a value indicating whether unknown content encodings should be rejected.
    /// </summary>
    public bool RejectUnknownContentEncoding { get; set; } = true;

    /// <summary>
    /// Gets the per-part rules.
    /// </summary>
    public List<KrPartRule> Rules { get; } = [];

    /// <summary>
    /// Gets or sets the hook invoked for each part.
    /// </summary>
    public Func<KrPartContext, ValueTask<KrPartAction>>? OnPart { get; set; }

    /// <summary>
    /// Gets or sets the hook invoked after parsing completes.
    /// </summary>
    public Func<KrFormContext, ValueTask<object?>>? OnCompleted { get; set; }

    /// <summary>
    /// Gets or sets the logger used by the form parser.
    /// </summary>
    public Serilog.ILogger? Logger { get; set; }
}

/// <summary>
/// Defines form parsing limits.
/// </summary>
public sealed class KrFormLimits
{
    /// <summary>
    /// Gets or sets the maximum allowed request body size in bytes.
    /// </summary>
    public long? MaxRequestBodyBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum allowed part body size in bytes.
    /// </summary>
    public long MaxPartBodyBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of parts allowed.
    /// </summary>
    public int MaxParts { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the maximum header bytes per part.
    /// </summary>
    public int MaxHeaderBytesPerPart { get; set; } = 16 * 1024;

    /// <summary>
    /// Gets or sets the maximum field value size in bytes.
    /// </summary>
    public long MaxFieldValueBytes { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets or sets the maximum nesting depth for multipart bodies.
    /// </summary>
    public int MaxNestingDepth { get; set; } = 1;
}
