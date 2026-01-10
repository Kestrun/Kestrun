/// <summary>
/// OpenAPI Parameter metadata for query/path/header/cookie items.
/// Apply on parameters or fields marked with <see cref="OpenApiModelKindAttribute"/> = Parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiParameterAttribute : OpenApiProperties
{
    /// <summary>Where the parameter lives (query/header/path/cookie).</summary>
    public string In { get; set; } = OaParameterLocation.Query.ToString();
    /// <summary>Override the parameter name (default: property name).</summary>
    public string? Name { get; set; }
    /// <summary>Override the parameter name (default: property name).</summary>
    public string? Key { get; set; }
    /// <summary>Marks the parameter as required.</summary>
    public bool Required { get; set; }

    /// <summary>Allow empty value (query only).</summary>
    public bool AllowEmptyValue { get; set; }
    /// <summary>Serialization style hint.</summary>
    public OaParameterStyle? Style { get; set; }
    /// <summary>Explode hint for structured values.</summary>
    public bool Explode { get; set; }
    /// <summary>Allow reserved characters unescaped (query only).</summary>
    public bool AllowReserved { get; set; }
}
