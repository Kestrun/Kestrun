
/// <summary>
/// Specifies metadata for an OpenAPI response object.
/// Can be attached to PowerShell or C# classes representing reusable responses.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiResponseAttribute : KestrunAnnotation, IOpenApiResponseAttribute
{
    /// <summary>
    /// Optional component name override for components.responses key.
    /// If omitted, the generator will name by member (Class.Property) when used on members,
    /// or by class name when applied at class-level.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// The HTTP status code (e.g., "200", "400", "404").
    /// </summary>
    public string StatusCode { get; set; } = "default";

    /// <summary>
    /// A short description of the response (required in OpenAPI spec).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional reference to a predefined schema (e.g., "UserInfoResponse").
    /// </summary>
    public string? SchemaRef { get; set; }

    /// <summary>
    /// The schema defining the type used for the items in an array media type. This property is only applicable for OAS 3.2.0 and later.
    /// </summary>
    public string? ItemSchemaRef { get; set; }

    /// <summary>
    /// Optional CLR type for the response schema.
    /// When set, this takes precedence over SchemaRef and is
    /// mapped via InferPrimitiveSchema / PrimitiveSchemaMap.
    /// </summary>
    public Type? Schema { get; set; }

    /// <summary>
    /// MIME type of the response payload (default: "application/json").
    /// </summary>
    public string[] ContentType { get; set; } = ["application/json"];

    /// <summary>
    /// If true, the schema will be inlined rather than referenced.
    /// </summary>
    public bool Inline { get; set; }
}
