/// <summary>
/// OpenAPI XML metadata for properties and classes.
/// Use this attribute to configure XML-specific serialization details in OpenAPI 3.2+.
/// Apply to:
/// <list type="bullet">
/// <item><description>Class (object-level): set XML name, namespace, and prefix for the entire schema.</description></item>
/// <item><description>Property (member-level): set XML name, whether it's an attribute, wrapping behavior, etc.</description></item>
/// </list>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiXmlAttribute : KestrunAnnotation
{
    /// <summary>
    /// Replaces the name of the element/attribute used for the described schema property.
    /// When applied to arrays, applies to the individual items (not the array wrapper).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The absolute URI of the namespace definition.
    /// Example: "http://example.com/schema/v1"
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// A prefix for the name (used with namespace).
    /// Example: "sample" would result in "sample:elementName"
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// Declares whether the property is rendered as an XML attribute instead of an element.
    /// Only applies to properties, not to arrays or complex types.
    /// Default: false (rendered as element).
    /// </summary>
    public bool Attribute { get; set; }

    /// <summary>
    /// Indicates whether array items should be wrapped in an enclosing element.
    /// Only applies to array properties.
    /// Default: false (unwrapped).
    /// </summary>
    public bool Wrapped { get; set; }
}
