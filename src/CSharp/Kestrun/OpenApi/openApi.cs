// File: OpenApiAttributes.cs
// Target: .NET 8+
// Note: Placed in the GLOBAL namespace (no 'namespace { }' block) so PowerShell can use
// [OpenApiSchema] / [OpenApiParameter] without `using namespace ...`.
//
// To generate XML docs, enable in your .csproj:
//   <PropertyGroup>
//     <GenerateDocumentationFile>true</GenerateDocumentationFile>
//   </PropertyGroup>

#pragma warning disable CA1050 // Declare types in namespaces

/// <summary>
/// Kind of OpenAPI model a class represents.
/// <para><see cref="Schema"/> -> object used as request/response body schema (components/schemas)</para>
/// <para><see cref="Parameters"/> -> container of operation parameters (query/path/header/cookie)</para>
/// </summary>
public enum OpenApiModelKind

{
    /// <summary>Class represents an object schema for request/response bodies.</summary>
    Schema = 0,
    /// <summary>Class represents a set of operation parameters (query/path/header/cookie).</summary>
    Parameters = 1
}

/// <summary>
/// Marks a class as <see cref="OpenApiModelKind.Schema"/> (body) or
/// <see cref="OpenApiModelKind.Parameters"/> (operation parameters).
/// </summary>
/// <remarks>Create the attribute with a specific model kind.</remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiModelKindAttribute(OpenApiModelKind kind) : Attribute
{

    /// <summary>The kind of OpenAPI model this class represents.</summary>
    public OpenApiModelKind Kind { get; } = kind;
}


/// <summary>
/// OpenAPI Schema primitive/object kinds.
/// </summary>
public enum OaSchemaType
{
    /// <summary>Let the generator infer from .NET type.</summary>
    None = 0,
    /// <summary>string</summary>
    String,
    /// <summary>number</summary>
    Number,
    /// <summary>integer</summary>
    Integer,
    /// <summary>boolean</summary>
    Boolean,
    /// <summary>array</summary>
    Array,
    /// <summary>object</summary>
    Object
}

/// <summary>
/// Location of an OpenAPI parameter.
/// </summary>
public enum OaParameterLocation
{
    /// <summary>Query string parameter (?x=1)</summary>
    Query = 0,
    /// <summary>Header parameter</summary>
    Header = 1,
    /// <summary>Path parameter (/users/{id})</summary>
    Path = 2,
    /// <summary>Cookie parameter</summary>
    Cookie = 3
}

/// <summary>
/// Serialization style hints for parameters (per OAS 3.x).
/// </summary>
public enum OaParameterStyle
{
    /// <summary>Default for path &amp; header (no delimiters).</summary>
    Simple = 0,
    /// <summary>Default for query &amp; cookie.</summary>
    Form,
    /// <summary>Matrix style for path; e.g., ;color=blue.</summary>
    Matrix,
    /// <summary>Label style for path; e.g., .blue.</summary>
    Label,
    /// <summary>Space-delimited arrays in query.</summary>
    SpaceDelimited,
    /// <summary>Pipe-delimited arrays in query.</summary>
    PipeDelimited,
    /// <summary>Deep object style for nested objects in query.</summary>
    DeepObject
}

/// <summary>
/// Rich OpenAPI schema metadata for a property or a class.
/// Apply to:
/// <list type="bullet">
/// <item><description>Class (object-level): set <see cref="Required"/> array, XML hints, discriminator, etc.</description></item>
/// <item><description>Property (member-level): set description, format, constraints, enum, etc.</description></item>
/// </list>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class OpenApiSchemaAttribute : Attribute
{
    // ---- Basic ----
    /// <summary>Human-friendly title for the schema.</summary>
    public string? Title { get; set; }
    /// <summary>Markdown-capable description.</summary>
    public string? Description { get; set; }
    /// <summary>Explicit OpenAPI type override (otherwise inferred).</summary>
    public OaSchemaType Type { get; set; } = OaSchemaType.None;
    /// <summary>OpenAPI format hint (e.g., "email", "uri", "date-time").</summary>
    public string? Format { get; set; }
    /// <summary>Default value.</summary>
    public object? Default { get; set; }
    /// <summary>Example value (single example).</summary>
    public object? Example { get; set; }
    /// <summary>Marks the schema as nullable (OAS 3.0).</summary>
    public bool Nullable { get; set; }
    /// <summary>Indicates the value is read-only (responses only).</summary>
    public bool ReadOnly { get; set; }
    /// <summary>Indicates the value is write-only (requests only).</summary>
    public bool WriteOnly { get; set; }
    /// <summary>Marks the schema/property as deprecated.</summary>
    public bool Deprecated { get; set; }

    // ---- Numbers ----
    /// <summary>Value must be a multiple of this.</summary>
    public decimal? MultipleOf { get; set; }
    /// <summary>Inclusive maximum.</summary>
    public string? Maximum { get; set; }
    /// <summary>Exclusive maximum flag.</summary>
    public bool ExclusiveMaximum { get; set; }
    /// <summary>Inclusive minimum.</summary>
    public string? Minimum { get; set; }
    /// <summary>Exclusive minimum flag.</summary>
    public bool ExclusiveMinimum { get; set; }

    // ---- Strings ----
    /// <summary>Maximum length (characters).</summary>
    public int MaxLength { get; set; } = -1;
    /// <summary>Minimum length (characters).</summary>
    public int MinLength { get; set; } = -1;
    /// <summary>ECMA-262 compliant regex.</summary>
    public string? Pattern { get; set; }

    // ---- Arrays ----
    /// <summary>Max items in array.</summary>
    public int MaxItems { get; set; } = -1;
    /// <summary>Min items in array.</summary>
    public int MinItems { get; set; } = -1;
    /// <summary>Items must be unique.</summary>
    public bool UniqueItems { get; set; }

    // ---- Objects ----
    /// <summary>Max properties an object may contain.</summary>
    public int MaxProperties { get; set; } = -1;
    /// <summary>Min properties an object must contain.</summary>
    public int MinProperties { get; set; } = -1;
    /// <summary>
    /// Object-level required property names (apply this on the CLASS).
    /// </summary>
    public string[]? Required { get; set; }

    // ---- Enum ----
    /// <summary>Allowed constant values for the schema.</summary>
    public object[]? Enum { get; set; }

    // ---- Array typing ----
    /// <summary>Items type by OpenAPI reference (e.g., "#/components/schemas/Address").</summary>
    public string? ItemsRef { get; set; }
    /// <summary>Items type by .NET type for code-first generators.</summary>
    public Type? ItemsType { get; set; }

    // ---- additionalProperties (map/dictionary) ----
    /// <summary>Allow or disallow additional properties (null = generator decides).</summary>
    public bool? AdditionalPropertiesAllowed { get; set; }
    /// <summary>Schema reference for additionalProperties.</summary>
    public string? AdditionalPropertiesRef { get; set; }
    /// <summary>.NET type to infer additionalProperties schema.</summary>
    public Type? AdditionalPropertiesType { get; set; }

    // ---- Composition ----
    /// <summary>oneOf refs (by $ref).</summary>
    public string[]? OneOfRefs { get; set; }
    /// <summary>anyOf refs (by $ref).</summary>
    public string[]? AnyOfRefs { get; set; }
    /// <summary>allOf refs (by $ref).</summary>
    public string[]? AllOfRefs { get; set; }
    /// <summary>not ref (by $ref).</summary>
    public string? NotRef { get; set; }
    /// <summary>oneOf types (by .NET type).</summary>
    public Type[]? OneOfTypes { get; set; }
    /// <summary>anyOf types (by .NET type).</summary>
    public Type[]? AnyOfTypes { get; set; }
    /// <summary>allOf types (by .NET type).</summary>
    public Type[]? AllOfTypes { get; set; }
    /// <summary>not type (by .NET type).</summary>
    public Type? NotType { get; set; }

    // ---- Discriminator ----
    /// <summary>Name of the property used to discriminate between schemas.</summary>
    public string? DiscriminatorPropertyName { get; set; }
    /// <summary>Payload values matched by the discriminator.</summary>
    public string[]? DiscriminatorMappingKeys { get; set; }
    /// <summary>Schema $refs that correspond to the mapping keys.</summary>
    public string[]? DiscriminatorMappingRefs { get; set; }

    // ---- External Docs ----
    /// <summary>External documentation URL.</summary>
    public string? ExternalDocsUrl { get; set; }
    /// <summary>Description for the external docs.</summary>
    public string? ExternalDocsDescription { get; set; }

    // ---- XML ----
    /// <summary>XML element/attribute name.</summary>
    public string? XmlName { get; set; }
    /// <summary>XML namespace.</summary>
    public string? XmlNamespace { get; set; }
    /// <summary>XML prefix.</summary>
    public string? XmlPrefix { get; set; }
    /// <summary>Indicates the property is an XML attribute.</summary>
    public bool XmlAttribute { get; set; }
    /// <summary>Indicates arrays are wrapped in an enclosing element.</summary>
    public bool XmlWrapped { get; set; }
}

/// <summary>
/// OpenAPI Parameter metadata for query/path/header/cookie items.
/// Apply on properties inside a class marked with <see cref="OpenApiModelKindAttribute"/> = Parameters.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class OpenApiParameterAttribute : Attribute
{
    /// <summary>Where the parameter lives (query/header/path/cookie).</summary>
    public OaParameterLocation In { get; set; } = OaParameterLocation.Query;
    /// <summary>Override the parameter name (default: property name).</summary>
    public string? Name { get; set; }
    /// <summary>Marks the parameter as required.</summary>
    public bool Required { get; set; }
    /// <summary>Marks the parameter as deprecated.</summary>
    public bool Deprecated { get; set; }
    /// <summary>Allow empty value (query only).</summary>
    public bool AllowEmptyValue { get; set; }
    /// <summary>Serialization style hint.</summary>
    public OaParameterStyle? Style { get; set; }
    /// <summary>Explode hint for structured values.</summary>
    public bool? Explode { get; set; }
    /// <summary>Allow reserved characters unescaped (query only).</summary>
    public bool AllowReserved { get; set; }
    /// <summary>Example value (single example).</summary>
    public object? Example { get; set; }
}


/// <summary>Repeat on a class to mark required property names (PowerShell-friendly).</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiRequiredAttribute(string name) : Attribute
{
    /// <summary>The name of the required property.</summary>
    public string Name { get; } = name;
}

/// <summary>Place on a property to mark it as required (PowerShell-friendly).</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiRequiredPropertyAttribute : Attribute { }
#pragma warning restore CA1050 // Declare types in namespaces


