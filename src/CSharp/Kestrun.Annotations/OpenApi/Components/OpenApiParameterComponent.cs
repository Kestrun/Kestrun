/// <summary>
/// Specifies metadata for an OpenAPI Parameter object. Can be applied to classes
/// to contribute entries under components.parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiParameterComponent : KestrunAnnotation
{
    public OaParameterLocation In { get; set; } = OaParameterLocation.Query;
    /// <summary>Override the parameter name (default: property name).</summary>
    public string? Name { get; set; }
    /// <summary>
    /// Optional description for the parameter.
    /// </summary>
    public string? Description { get; set; }
    /// <summary>Override the parameter name (default: property name).</summary>
    public string? Key { get; set; }
    /// <summary>Marks the parameter as required.</summary>
    public bool Required { get; set; }
    /// <summary>Marks the parameter as deprecated.</summary>
    public bool Deprecated { get; set; }
    /// <summary>Allow empty value (query only).</summary>
    public bool AllowEmptyValue { get; set; }
    /// <summary>Serialization style hint.</summary>
    public OaParameterStyle? Style { get; set; }
    /// <summary>Explode hint for structured values.</summary>
    public bool Explode { get; set; }
    /// <summary>Allow reserved characters unescaped (query only).</summary>
    public bool AllowReserved { get; set; }
    /// <summary>Example value (single example).</summary>
    public object? Example { get; set; }

    // Todo: Remove it
    public string? JoinClassName { get; set; }
}
