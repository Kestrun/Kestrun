/// <summary>
/// Specifies metadata for an OpenAPI Header object. Can be applied to classes
/// to contribute entries under components.headers.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiSchemaComponent : KestrunAnnotation
{
    /// <summary>
    /// Optional component key override. If omitted, generator will use class/member naming rules.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Description for the request body.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether this schema is deprecated.
    /// </summary>
    public bool Deprecated { get; set; }


    /// <summary>
    /// Inline example object for the media type (optional). If omitted and the member has a
    /// default value, the generator will use that default as the example.
    /// </summary>
    public object? Example { get; set; }

    /// <summary>
    /// Comma-separated list of required properties for this schema.
    /// </summary>
    public string? Required { get; set; }


    /// <summary>
    /// Indicates whether array items must be unique.
    /// </summary>
    public bool UniqueItems { get; set; }

    public bool AdditionalPropertiesAllowed { get; set; } = true;
    /// <summary>
    /// Inline examples object for the schema (optional).
    /// </summary>
    public object? Examples { get; set; }

    /// <summary>
    /// Title for the schema.
    /// </summary>
    public string? Title { get; set; }
}
