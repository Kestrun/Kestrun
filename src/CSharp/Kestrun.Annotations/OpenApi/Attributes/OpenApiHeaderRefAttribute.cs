/// <summary>
/// Place on a property or field to indicate it is a link reference.
/// </summary>
/// <param name="key">The local name under response.links</param>
/// <param name="refId">The components/links id</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public sealed class OpenApiHeaderRefAttribute : KestrunAnnotation
{
    /// <summary>
    /// The HTTP status code (e.g., "200", "400", "404").
    /// This is only used when applied to method parameters to
    /// associate the property with a specific response.
    /// </summary>
    public string? StatusCode { get; set; }
    /// <summary>
    /// The local name under components.headers.
    /// </summary>
    public required string Key { get; set; }
    /// <summary>
    /// The reference ID under components.headers.
    /// </summary>
    public required string ReferenceId { get; set; }
}
