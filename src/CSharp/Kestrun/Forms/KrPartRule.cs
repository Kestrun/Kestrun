namespace Kestrun.Forms;

/// <summary>
/// Defines a rule for a named multipart part.
/// </summary>
public sealed class KrFormPartRule
{
    /// <summary>
    /// Gets or sets the part name to match.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the scope name for nested multipart rules. When not set, the rule applies only at the root.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets the description of the part.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the part is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether multiple parts with the same name are allowed.
    /// </summary>
    public bool AllowMultiple { get; set; } = true;

    /// <summary>
    /// Gets or sets the allowed content types for the part.
    /// </summary>
    public List<string> AllowedContentTypes { get; } = [];

    /// <summary>
    /// Gets or sets the allowed file extensions for file parts.
    /// </summary>
    public List<string> AllowedExtensions { get; } = [];

    /// <summary>
    /// Gets or sets the maximum number of bytes allowed for the part.
    /// </summary>
    public long? MaxBytes { get; set; }

    /// <summary>
    /// Gets or sets the decode mode for the part.
    /// </summary>
    public KrPartDecodeMode DecodeMode { get; set; } = KrPartDecodeMode.None;

    /// <summary>
    /// Gets or sets the destination path override for the part.
    /// </summary>
    public string? DestinationPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the part should be stored to disk.
    /// </summary>
    public bool StoreToDisk { get; set; } = true;
}

/// <summary>
/// Represents decode mode options for parts (scaffold only).
/// </summary>
public enum KrPartDecodeMode
{
    /// <summary>
    /// No decoding.
    /// </summary>
    None,

    /// <summary>
    /// Decode as UTF-8 text.
    /// </summary>
    TextUtf8,

    /// <summary>
    /// Decode as JSON (placeholder).
    /// </summary>
    Json,

    /// <summary>
    /// Decode as Base64 (placeholder).
    /// </summary>
    Base64,

    /// <summary>
    /// Decode as Base64 URL (placeholder).
    /// </summary>
    Base64Url
}
