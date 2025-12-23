/// <summary>
/// Place on a property or field to indicate it is a parameter reference.
/// </summary>
/// <param name="name">The name of the parameter.</param>
/// <param name="referenceId">The components/parameters id.</param>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiParameterRefAttribute : KestrunAnnotation
{
    /// <summary>
    /// The name of the parameter.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The reference ID under components/parameters.
    /// </summary>
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
