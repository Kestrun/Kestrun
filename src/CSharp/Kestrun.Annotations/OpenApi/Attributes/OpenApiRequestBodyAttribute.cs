/// <summary>
/// Specifies metadata for an OpenAPI Request Body component. Can be applied to parameters
/// to contribute entries under components.requestBodies.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiRequestBodyAttribute : KestrunAnnotation
{
    /// <summary>Description for the request body.</summary>
    public string? Description { get; set; }

    /// <summary>Media type. Defaults to application/json.</summary>
    public string[] ContentType { get; set; } = ["application/json"];

    /// <summary>Whether the request body is required.</summary>
    public bool Required { get; set; }

    /// <summary>
    /// Inline example object for the media type (optional). If omitted and the member has a
    /// default value, the generator will use that default as the example.
    /// </summary>
    public object? Example { get; set; }

    /// <summary>
    /// When true, the generator will emit an inline schema object for the request body instead
    /// of a <c>$ref</c> to <c>components.schemas</c>. This applies whether the schema is resolved
    /// from a CLR type or from a string-based schema reference; in both cases the resolved schema
    /// will be inlined where supported.
    /// </summary>
    public bool Inline { get; set; }
}
