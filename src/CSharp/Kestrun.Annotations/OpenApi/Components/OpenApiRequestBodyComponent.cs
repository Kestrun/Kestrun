/// <summary>
/// Specifies metadata for an OpenAPI Header object. Can be applied to classes
/// to contribute entries under components.headers.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiRequestBodyComponentAttribute : OpenApiProperties
{

    /// <summary>
    /// Description of the request body.
    /// </summary>
    public new string? Description { get; set; }

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
    public bool Required { get; set; }

    /// <summary>
    /// When true, the generator will emit an inline schema object for the request body instead
    /// of a <c>$ref</c> to <c>components.schemas</c>. This applies whether the schema is resolved
    /// from a CLR type or from a string-based schema reference; in both cases the resolved schema
    /// will be inlined where supported.
    /// </summary>
    public bool Inline { get; set; }
}
