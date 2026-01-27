namespace Kestrun.Forms;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="KrFormLimits"/> class.
    /// </summary>
    public KrFormLimits() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="KrFormLimits"/> class by copying settings from another instance.
    /// </summary>
    /// <param name="other"> The instance to copy settings from. </param>
    public KrFormLimits(KrFormLimits other)
    {
        MaxRequestBodyBytes = other.MaxRequestBodyBytes;
        MaxPartBodyBytes = other.MaxPartBodyBytes;
        MaxParts = other.MaxParts;
        MaxHeaderBytesPerPart = other.MaxHeaderBytesPerPart;
        MaxFieldValueBytes = other.MaxFieldValueBytes;
        MaxNestingDepth = other.MaxNestingDepth;
    }

    /// <summary>
    /// Returns a string representation of the form limits.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => $"MaxRequestBodyBytes={MaxRequestBodyBytes}, MaxPartBodyBytes={MaxPartBodyBytes}, MaxParts={MaxParts}, MaxHeaderBytesPerPart={MaxHeaderBytesPerPart}, MaxFieldValueBytes={MaxFieldValueBytes}, MaxNestingDepth={MaxNestingDepth}";
}
