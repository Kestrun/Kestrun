/// <summary>
/// Specifies metadata for an OpenAPI Header component. Can be applied to classes,
/// properties, or fields to contribute entries under components.headers.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiResponseHeaderAttribute : KestrunAnnotation, IOpenApiResponseHeaderAttribute
{
    /// <inheritdoc/>
    public string StatusCode { get; set; } = "default";

    /// <summary>Optional component key override. If omitted, generator will use class/member naming rules.</summary>
    public required string Key { get; set; }

    /// <summary>Header description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the header is required.</summary>
    public bool Required { get; set; }

    /// <summary>Whether the header is deprecated.</summary>
    public bool Deprecated { get; set; }

    /// <summary>Whether the header allows empty value.</summary>
    public bool AllowEmptyValue { get; set; }

    /// <summary>Serialization style hint.</summary>
    public OaParameterStyle? Style { get; set; }

    /// <summary>Explode flag for serialization.</summary>
    public bool Explode { get; set; }

    /// <summary>
    /// Whether the header allows reserved characters.
    /// </summary>
    public bool AllowReserved { get; set; }

    /// <summary>Inline example for header value.</summary>
    public object? Example { get; set; }

    /// <summary>
    /// Type of the schema for the header value.
    /// </summary>
    public Type? Schema { get; set; }
}
