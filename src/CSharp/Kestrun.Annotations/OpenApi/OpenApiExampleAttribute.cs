/// <summary>
/// Specifies metadata for an OpenAPI Example object. Can be applied to classes,
/// properties, or fields to contribute entries under components.examples.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiExampleAttribute : Attribute
{
    /// <summary>
    /// Optional component name override for components.examples key.
    /// If omitted, generator uses class name (class-level) or member name (member-level),
    /// possibly prefixed by class name depending on JoinClassName.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Short summary for the example.</summary>
    public string? Summary { get; set; }

    /// <summary>Longer description for the example.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// External example URI. If provided, Value is usually omitted per spec.
    /// </summary>
    public string? ExternalValue { get; set; }
}
