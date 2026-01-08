/// <summary>
/// Declares a reusable example on a specific media type of a parameter,
/// mapping a local example key to a components/examples reference.
/// </summary>
/// <remarks>
/// Create an example reference specifying the media type.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class OpenApiParameterExampleRefAttribute : KestrunAnnotation, IOpenApiExampleAttribute
{
    /// <summary>Local name under content[contentType].examples</summary>
    public string Key { get; set; } = "application/json";

    /// <summary>Id under #/components/examples/{ReferenceId}</summary>
    public required string ReferenceId { get; set; }

    /// <summary>
    /// When true, embeds the example directly instead of referencing it.
    /// </summary>
    public bool Inline { get; set; }
}
