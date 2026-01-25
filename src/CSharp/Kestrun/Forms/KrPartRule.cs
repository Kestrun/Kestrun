namespace Kestrun.Forms;

/// <summary>
/// Represents a rule for validating and processing a specific part.
/// </summary>
public sealed record KrPartRule
{
    /// <summary>
    /// Gets the name of the part this rule applies to (required for named parts).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether this part is required.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Gets a value indicating whether multiple instances of this part are allowed.
    /// </summary>
    public bool AllowMultiple { get; init; }

    /// <summary>
    /// Gets the allowed content types for this part (supports wildcards like image/*).
    /// </summary>
    public List<string>? AllowedContentTypes { get; init; }

    /// <summary>
    /// Gets the allowed file extensions for this part (e.g., .jpg, .png).
    /// </summary>
    public List<string>? AllowedExtensions { get; init; }

    /// <summary>
    /// Gets the maximum bytes for this specific part (overrides global MaxPartBodyBytes).
    /// </summary>
    public long? MaxBytes { get; init; }

    /// <summary>
    /// Gets the decode mode for this part (placeholder for future expansion).
    /// </summary>
    public KrPartDecodeMode DecodeMode { get; init; } = KrPartDecodeMode.None;

    /// <summary>
    /// Gets the destination path override for this part.
    /// </summary>
    public string? DestinationPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether to store this part to disk (default true).
    /// </summary>
    public bool StoreToDisk { get; init; } = true;
}

/// <summary>
/// Specifies the decoding mode for a part (placeholder for future expansion).
/// </summary>
public enum KrPartDecodeMode
{
    /// <summary>
    /// No decoding (store raw bytes).
    /// </summary>
    None,

    /// <summary>
    /// Decode as UTF-8 text.
    /// </summary>
    TextUtf8,

    /// <summary>
    /// Decode as JSON.
    /// </summary>
    Json,

    /// <summary>
    /// Decode as Base64.
    /// </summary>
    Base64,

    /// <summary>
    /// Decode as Base64Url.
    /// </summary>
    Base64Url
}
