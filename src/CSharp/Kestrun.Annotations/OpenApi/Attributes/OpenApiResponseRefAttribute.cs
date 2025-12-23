/// <summary>
/// Place on a property or field to indicate it is a response reference.
/// </summary>
/// <param name="statusCode">The HTTP status code for the response.</param>
/// <param name="refId">The components/responses id</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiResponseRefAttribute : KestrunAnnotation, IOpenApiResponseAttribute
{
    /// <summary>
    /// The HTTP status code for the response.
    /// </summary>
    public required string StatusCode { get; set; }

    /// <summary>
    /// The components/responses id
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
