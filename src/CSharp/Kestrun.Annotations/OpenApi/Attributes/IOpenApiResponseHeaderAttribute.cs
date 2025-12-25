/// <summary>
/// Interface for OpenAPI Response Header attributes.
/// </summary>
public interface IOpenApiResponseHeaderAttribute
{
    /// <summary>
    /// The HTTP status code (e.g., "200", "400", "404").
    /// This is only used when applied to method parameters to
    /// associate the property with a specific response.
    /// </summary>
    string StatusCode { get; set; }

    /// <summary>
    /// The local name under components.headers.
    /// </summary>
    string Key { get; set; }
}
