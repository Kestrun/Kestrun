[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class KrBindFormAttribute : KestrunAnnotation
{
    /// <summary>
    /// Optional override: default upload path.
    /// </summary>
    public string? DefaultUploadPath { get; set; }

    /// <summary>
    /// Optional override: compute SHA-256 for file parts.
    /// </summary>
    public bool ComputeSha256 { get; set; }

    /// <summary>
    /// Optional override: enable per-part decompression.
    /// </summary>
    public bool EnablePartDecompression { get; set; }

    /// <summary>
    /// Optional override: max decompressed bytes per part.
    /// </summary>
    public long MaxDecompressedBytesPerPart { get; set; }

    /// <summary>
    /// Optional override: allowed request content types.
    /// </summary>
    public bool RejectUnknownRequestContentType { get; set; } = true;

    /// <summary>
    /// Optional override: allowed part content encodings for decompression.
    /// </summary>
    public string[]? AllowedPartContentEncodings { get; set; }

    /// <summary>
    /// Optional override: reject unknown part content encodings.
    /// </summary>
    public bool RejectUnknownContentEncoding { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum allowed request body size in bytes.
    /// </summary>
    public long MaxRequestBodyBytes { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed part body size in bytes.
    /// </summary>
    public long MaxPartBodyBytes { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of parts allowed.
    /// </summary>
    public int MaxParts { get; set; }

    /// <summary>
    /// Gets or sets the maximum header bytes per part.
    /// </summary>
    public int MaxHeaderBytesPerPart { get; set; }

    /// <summary>
    /// Gets or sets the maximum field value size in bytes.
    /// </summary>
    public long MaxFieldValueBytes { get; set; }

    /// <summary>
    /// Gets or sets the maximum nesting depth for multipart bodies.
    /// </summary>
    public int MaxNestingDepth { get; set; }
}
