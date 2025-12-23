/// <summary>
/// Place on a property or field to indicate it is a link reference.
/// </summary>
/// <param name="key">The local name under response.links</param>
/// <param name="refId">The components/links id</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public sealed class OpenApiLinkRefAttribute : KestrunAnnotation
{
    /// <summary>
    /// The HTTP status code for the response.
    /// </summary>
    public string? StatusCode { get; set; }
    public required string Key { get; set; }      // local name under response.links
    public required string ReferenceId { get; set; } // components/links id
}
