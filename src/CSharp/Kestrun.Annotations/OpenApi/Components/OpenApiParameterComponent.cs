/// <summary>
/// Specifies metadata for an OpenAPI Parameter object. Can be applied to classes
/// to contribute entries under components.parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiParameterComponent : OpenApiProperties
{
    /// <summary>
    /// Title is not supported for parameter components.
    /// </summary>
    [Obsolete("Title is not supported for parameter components.", error: false)]
    public new string? Title
    {
        get => base.Title;
        set => throw new NotSupportedException("Title is not supported for OpenApiParameterComponent.");
    }

    /// <summary>
    /// The location of the parameter.
    /// </summary>
    public OaParameterLocation In { get; set; } = OaParameterLocation.Query;

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
