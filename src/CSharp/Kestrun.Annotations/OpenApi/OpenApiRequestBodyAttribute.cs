#pragma warning disable CA1050 // Declare types in namespaces

/// <summary>
/// Specifies metadata for an OpenAPI Request Body component. Can be applied to classes,
/// properties, or fields to contribute entries under components.requestBodies.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiRequestBodyAttribute : Attribute
{
    /// <summary>Optional component key override. If omitted, generator will use class/member naming rules.</summary>
    public string? Name { get; set; }

    /// <summary>Description for the request body.</summary>
    public string? Description { get; set; }

    /// <summary>Media type. Defaults to application/json.</summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>Schema component name to reference (from components.schemas).</summary>
    public string? SchemaRef { get; set; }

    /// <summary>Whether the request body is required.</summary>
    public bool Required { get; set; }

    /// <summary>
    /// Inline example object for the media type (optional). If omitted and the member has a
    /// default value, the generator will use that default as the example.
    /// </summary>
    public object? Example { get; set; }

    /// <summary>
    /// When true, emit an inline schema object instead of a $ref. If SchemaRef is provided and
    /// a matching schema exists in components, that schema will be embedded; otherwise the generator
    /// will try to infer an inline schema when possible.
    /// </summary>
    public bool Inline { get; set; }

}
#pragma warning restore CA1050 // Declare types in namespaces
