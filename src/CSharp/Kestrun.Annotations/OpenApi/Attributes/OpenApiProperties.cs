
/// <summary>
/// Rich OpenAPI schema metadata for a property or a class.
/// Apply to:
/// <list type="bullet">
/// <item><description>Class (object-level): set <see cref="RequiredProperties"/> array, XML hints, discriminator, etc.</description></item>
/// <item><description>Property (member-level): set description, format, constraints, enum, etc.</description></item>
/// </list>
/// </summary>
public abstract class OpenApiProperties : KestrunAnnotation
{
    /// <summary>
    /// Human-friendly title for the schema.
    /// </summary>
    public string? Title { get; set; }
    /// <summary>Markdown-capable description.</summary>
    public string? Description { get; set; }

    /// <summary>Explicit OpenAPI type override (otherwise inferred).</summary>
    public OaSchemaType Type { get; set; } = OaSchemaType.None;
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
    /// <summary>
    /// Indicates the property is an array (enables array constraints).
    /// </summary>
    public bool Array { get; set; }
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
    public string[]? RequiredProperties { get; set; }

    // ---- Enum ----
    /// <summary>Allowed constant values for the schema.</summary>
    public object[]? Enum { get; set; }

    // ---- Array typing ----
    /// <summary>Items type by OpenAPI reference (e.g., "#/components/schemas/Address").</summary>
    //public string? ItemsRef { get; set; }
    /// <summary>Items type by .NET type for code-first generators.</summary>
    public Type? ItemsType { get; set; }

    /// <summary>
    ///  Indicates whether additionalProperties are allowed (default: false).
    /// </summary>
    public bool AdditionalPropertiesAllowed { get; set; } = true;

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
    /// <summary>
    /// Sets unevaluatedProperties for OpenAPI Schema (null = generator decides).
    /// </summary>
    public bool UnevaluatedProperties { get; set; }
}
