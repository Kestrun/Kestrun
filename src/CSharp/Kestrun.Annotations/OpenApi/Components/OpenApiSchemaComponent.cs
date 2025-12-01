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

    /// <summary>
    /// Inline examples object for the schema (optional).
    /// </summary>
    public object? Examples { get; set; }

    /// <summary>
    /// Title for the schema.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Indicates whether additional properties are allowed on this schema.
    /// </summary>
    public bool AdditionalPropertiesAllowed { get; set; } = true;

    /// <summary>
    /// OpenAPI "type" for the additionalProperties value schema
    /// (e.g. "string", "integer", "object", "array").
    /// Ignored if <see cref="AdditionalPropertiesRef"/> is set
    /// or <see cref="AdditionalPropertiesClrType"/> is used.
    /// </summary>
    public string? AdditionalPropertiesType { get; set; }

    /// <summary>
    /// OpenAPI "format" for the additionalProperties value schema
    /// (e.g. "int32", "int64", "date-time").
    /// </summary>
    public string? AdditionalPropertiesFormat { get; set; }

    /// <summary>
    /// Schema reference for the additionalProperties value schema.
    /// Can be a bare component key (e.g. "Pet") or a full pointer
    /// (e.g. "#/components/schemas/Pet").
    /// When set, this takes precedence over type/format/CLR type.
    /// </summary>
    public string? AdditionalPropertiesRef { get; set; }

    /// <summary>
    /// If true, the additionalProperties value schema will be an array of the
    /// specified type/ref (i.e. 'type: array' with 'items' = that schema).
    /// </summary>
    public bool AdditionalPropertiesIsArray { get; set; }

    /// <summary>
    /// If true, the additionalProperties value schema will be marked as nullable.
    /// </summary>
    public bool AdditionalPropertiesNullable { get; set; }

    /// <summary>
    /// Optional description for the additionalProperties value schema.
    /// </summary>
    public string? AdditionalPropertiesDescription { get; set; }

    /// <summary>
    /// Optional enum values for the additionalProperties value schema.
    /// Typically used with 'AdditionalPropertiesType = "string"'.
    /// </summary>
    public string[]? AdditionalPropertiesEnum { get; set; }

    /// <summary>
    /// CLR type hint for the additionalProperties value schema.
    /// The generator can use this to infer OpenAPI type/format or
    /// to resolve a schema component reference when the type maps
    /// to another annotated schema.
    /// Ignored when <see cref="AdditionalPropertiesRef"/> is set.
    /// </summary>
    public Type? AdditionalPropertiesClrType { get; set; }
}
