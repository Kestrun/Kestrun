/// <summary>
/// OpenAPI Parameter metadata for query/path/header/cookie items.
/// Apply on properties inside a class marked with <see cref="OpenApiModelKindAttribute"/> = Parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class OpenApiParameterAttribute : KestrunAnnotation
{
    /// <summary>Where the parameter lives (query/header/path/cookie).</summary>
    public string In { get; set; } = OaParameterLocation.Query.ToString();
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
}
