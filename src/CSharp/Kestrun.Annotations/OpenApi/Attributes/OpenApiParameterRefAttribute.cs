/// <summary>
/// Place on a property or field to indicate it is a response reference.
/// </summary>
/// <param name="name">The name of the parameter.</param>
/// <param name="refId">The components/parameters id</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public sealed class OpenApiParameterRefAttribute : KestrunAnnotation
{
    public required string Name { get; set; }
    public required string ReferenceId { get; set; }
    /// <summary>
    /// Description of the response reference.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// If true, the schema will be inlined rather than referenced.
    /// </summary>
    public bool Inline { get; set; }
}
