[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class InternalPowershellAttribute : KestrunAnnotation
{
    /// <summary>
    /// Gets the attribute's minimum range. From ValidateRangeAttribute.MinRange
    /// </summary>
    public string? MinRange { get; set; }
    /// <summary>
    /// Gets the attribute's maximum range. From ValidateRangeAttribute.MaxRange
    /// </summary>
    public string? MaxRange { get; set; }

    /// <summary>
    /// Gets the attribute's minimum length. From ValidateLengthAttribute.MinLength
    /// </summary>
    public int? MinLength { get; set; }
    /// <summary>
    /// Gets the attribute's maximum length. From ValidateLengthAttribute.MaxLength
    /// </summary>
    public int? MaxLength { get; set; }
    /// <summary>
    /// Gets the attribute's valid values. From ValidateSetAttribute.ValidValues
    /// </summary>
    public IList<string>? AllowedValues { get; set; }
    /// <summary>
    /// Gets or sets the regex pattern. From ValidatePatternAttribute.RegexPattern
    /// </summary>
    public string? RegexPattern { get; set; }
    /// <summary>
    /// Gets the minimum count. From ValidateCountAttribute.MinLength
    /// </summary>
    public int? MinItems { get; set; }
    /// <summary>
    /// <summary>
    /// Gets the maximum count. From ValidateCountAttribute.MaxLength
    /// </summary>
    public int? MaxItems { get; set; }
    /// <summary>
    /// Gets or sets whether to validate not null or empty. From ValidateNotNullOrEmptyAttribute
    /// </summary>
    public bool? ValidateNotNullOrEmptyAttribute { get; set; }
    /// <summary>
    /// Gets or sets whether to validate not null. From ValidateNotNullAttribute
    /// </summary>
    public bool? ValidateNotNullAttribute { get; set; }
    /// <summary>
    /// Gets or sets whether to validate not null or white space. From ValidateNotNullOrWhiteSpaceAttribute
    /// </summary>
    public bool? ValidateNotNullOrWhiteSpaceAttribute { get; set; }
}
