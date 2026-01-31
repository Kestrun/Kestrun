namespace Kestrun.Forms;

/// <summary>
/// Configures form parsing behavior for Kestrun.
/// </summary>
public sealed class KrFormOptions
{
    /// <summary>
    /// Gets or sets the name of the form parser.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the form parser.
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// Gets the allowed request content types.
    /// </summary>
    public List<string> AllowedRequestContentTypes { get; } =
    [
        "multipart/form-data"
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
    public string DefaultUploadPath { get; set; } = KestrunHostManager.Default?.Options.DefaultUploadPath ?? Path.Combine(Path.GetTempPath(), "kestrun-uploads");

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
    public List<KrFormPartRule> Rules { get; } = [];

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

    /// <summary>
    /// Initializes a new instance of the <see cref="KrFormOptions"/> class.
    /// </summary>
    public KrFormOptions() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="KrFormOptions"/> class by copying settings from another instance.
    /// </summary>
    /// <param name="copyFrom">The instance to copy settings from.</param>
    public KrFormOptions(KrFormOptions copyFrom)
    {
        Name = copyFrom.Name;
        Description = copyFrom.Description;
        AllowedRequestContentTypes.AddRange(copyFrom.AllowedRequestContentTypes);
        RejectUnknownRequestContentType = copyFrom.RejectUnknownRequestContentType;
        DefaultUploadPath = copyFrom.DefaultUploadPath;
        SanitizeFileName = copyFrom.SanitizeFileName;
        ComputeSha256 = copyFrom.ComputeSha256;
        EnablePartDecompression = copyFrom.EnablePartDecompression;
        AllowedPartContentEncodings.AddRange(copyFrom.AllowedPartContentEncodings);
        MaxDecompressedBytesPerPart = copyFrom.MaxDecompressedBytesPerPart;
        RejectUnknownContentEncoding = copyFrom.RejectUnknownContentEncoding;
        Rules.AddRange(copyFrom.Rules);
        OnPart = copyFrom.OnPart;
        OnCompleted = copyFrom.OnCompleted;
        Logger = copyFrom.Logger;
        // Copy limits
        Limits = new KrFormLimits(copyFrom.Limits);
    }
}
