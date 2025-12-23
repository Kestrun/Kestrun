/// <summary>
/// Declares a reusable example on a specific media type of a parameter,
/// mapping a local example key to a components/examples reference.
/// </summary>
/// <remarks>
/// Create an example reference specifying the media type.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiParameterExampleRefAttribute : KestrunAnnotation, IOpenApiExampleAttribute
{
    /// <summary>Local name under content[contentType].examples</summary>
    public required string Key { get; set; }

    /// <summary>Id under #/components/examples/{ReferenceId}</summary>
    public required string ReferenceId { get; set; }

    /// <summary>
    /// When true, embeds the example directly instead of referencing it.
    /// </summary>
    public bool Inline { get; set; }
}
