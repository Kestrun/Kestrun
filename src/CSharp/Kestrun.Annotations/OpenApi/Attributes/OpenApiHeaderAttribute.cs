/// <summary>
/// Specifies metadata for an OpenAPI Header component. Can be applied to classes,
/// properties, or fields to contribute entries under components.headers.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiHeaderAttribute : KestrunAnnotation
{
    /// <summary>
    /// The HTTP status code (e.g., "200", "400", "404").
    /// This is only used when applied to method parameters to
    /// associate the property with a specific response.
    /// </summary>
    public string? StatusCode { get; set; }

    /// <summary>Optional component key override. If omitted, generator will use class/member naming rules.</summary>
    public required string? Key { get; set; }

    /// <summary>Header description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the header is required.</summary>
    public bool Required { get; set; }

    /// <summary>Whether the header is deprecated.</summary>
    public bool Deprecated { get; set; }

    /// <summary>Whether the header allows empty value.</summary>
    public bool AllowEmptyValue { get; set; }

    /// <summary>Serialization style hint.</summary>
    public OaParameterStyle Style { get; set; } = OaParameterStyle.Simple;

    /// <summary>Explode flag for serialization.</summary>
    public bool Explode { get; set; }

    /// <summary>Schema reference (components.schemas) for the header value.</summary>
    public required string Type { get; set; }

    /// <summary>
    /// Format hint for the header value (e.g., "date-time", "uuid").
    /// </summary>
    public string? Format { get; set; }

    /// <summary>Inline example for header value.</summary>
    public object? Example { get; set; }

    /// <summary>
    /// When true, indicates that the header value allows reserved characters.
    /// </summary>
    public bool AllowReserved { get; set; }

    /// <summary>
    /// Optional reference to an example component (e.g., "ExampleUser").
    /// </summary>
    public string? ExampleRef { get; set; }

    /// <summary>
    /// Optional reference to a schema component (e.g., "HeaderSchema").
    /// </summary>
    public string? SchemaRef { get; set; }
}
