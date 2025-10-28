#pragma warning disable CA1050 // Declare types in namespaces
/// <summary>
/// Specifies metadata for an OpenAPI Header component. Can be applied to classes,
/// properties, or fields to contribute entries under components.headers.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiHeaderAttribute : Attribute
{
    /// <summary>Optional component key override. If omitted, generator will use class/member naming rules.</summary>
    public required string? Name { get; set; }

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
}
#pragma warning restore CA1050 // Declare types in namespaces
