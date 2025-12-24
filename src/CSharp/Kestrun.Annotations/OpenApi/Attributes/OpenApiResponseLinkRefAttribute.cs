/// <summary>
/// Place on a property or field to indicate it is a link reference.
/// </summary>
/// <param name="key">The local name under response.links</param>
/// <param name="refId">The components/links id</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiResponseLinkRefAttribute : KestrunAnnotation
{
    /// <summary>
    /// The HTTP status code for the response.
    /// </summary>
    public required string StatusCode { get; set; }
    public required string Key { get; set; }      // local name under response.links
    public required string ReferenceId { get; set; } // components/links id

    /// <summary>
    /// If true, the schema will be inlined rather than referenced.
    /// </summary>
    public bool Inline { get; set; }
}
