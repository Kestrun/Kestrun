/// <summary>
/// Declares a reusable example reference for a request body,
/// mapping a local example key to a components/examples reference.
/// </summary>
/// <remarks>
/// Create a request body example reference, optionally specifying the media type.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class OpenApiRequestBodyExampleRefAttribute : KestrunAnnotation, IOpenApiExampleAttribute
{
    /// <summary>Local name under content[contentType].examples</summary>
    public required string Key { get; set; }

    /// <summary>Id under #/components/examples/{ReferenceId}</summary>
    public required string ReferenceId { get; set; }

    /// <summary>Media type bucket (e.g., application/json, application/xml)</summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// When true, embeds the example directly instead of referencing it.
    /// </summary>
    public bool Inline { get; set; }
}
