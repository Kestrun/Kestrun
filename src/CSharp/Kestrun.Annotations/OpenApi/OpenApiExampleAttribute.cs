/// <summary>
/// Specifies metadata for an OpenAPI Example object. Can be applied to classes
/// to contribute entries under components.examples.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public sealed class OpenApiExampleAttribute : Attribute
{
    /// <summary>
    /// Optional component name override for components.examples key.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Short summary for the example.</summary>
    public string? Summary { get; set; }

    /// <summary>Longer description for the example.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Example value for the example.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// External example URI. If provided, Value is usually omitted per spec.
    /// </summary>
    public string? ExternalValue { get; set; }
}
