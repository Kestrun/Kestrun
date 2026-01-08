/// <summary>
/// Specifies metadata for an OpenAPI Parameter object. Can be applied to classes
/// to contribute entries under components.parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiParameterComponent : OpenApiProperties
{
#pragma warning disable IDE0051
    /// <summary>
    /// Not used. Hides Title from base class.
    /// </summary>
    private new string? Title { get; set; }
#pragma warning restore IDE0051

    public OaParameterLocation In { get; set; } = OaParameterLocation.Query;
    /// <summary>Override the parameter name (default: property name).</summary>
   // public string? Name { get; set; }
    /// <summary>Override the parameter name (default: property name).</summary>
   // public string? Key { get; set; }

    /// <summary>
    /// Whether the parameter is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>Allow empty value (query only).</summary>
    public bool AllowEmptyValue { get; set; }
    /// <summary>Serialization style hint.</summary>
    public OaParameterStyle? Style { get; set; }
    /// <summary>Explode hint for structured values.</summary>
    public bool Explode { get; set; }
    /// <summary>Allow reserved characters unescaped (query only).</summary>
    public bool AllowReserved { get; set; }
    /// <summary>
    /// Indicates that the parameter definition should be inlined
    /// into the operation rather than being a reusable component.
    /// </summary>
    public bool Inline { get; set; }

    /// <summary>The key is the media type. Will use content instead of schema.</summary>
    public string? ContentType { get; set; }
}
