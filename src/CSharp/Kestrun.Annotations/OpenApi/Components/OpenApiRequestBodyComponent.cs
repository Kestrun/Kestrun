/// <summary>
/// Specifies metadata for an OpenAPI Header object. Can be applied to classes
/// to contribute entries under components.headers.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiRequestBodyComponent : OpenApiProperties
{
    /// <summary>
    /// Optional component key override. If omitted, generator will use class/member naming rules.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Media type. Defaults to application/json.
    /// </summary>
    public string[] ContentType { get; set; } = ["application/json"];

    /// <summary>
    /// Whether the request body is required.
    /// </summary>
    public bool IsRequired { get; set; }
}
