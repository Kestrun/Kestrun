/// <summary>
/// Place on a property or field to indicate it is a request body reference.
/// </summary>
/// <param name="statusCode">The HTTP status code for the request body.</param>
/// <param name="refId">The components/requests id</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public sealed class OpenApiRequestBodyRefAttribute : KestrunAnnotation
{
    public required string StatusCode { get; set; }
    public required string ReferenceId { get; set; }
    /// <summary>
    /// Description of the request body reference.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// If true, the schema will be inlined rather than referenced.
    /// </summary>
    public bool Inline { get; set; }
    /// <summary>
    /// Indicates whether the request body is required.
    /// </summary>
    public bool Required { get; set; }
}
