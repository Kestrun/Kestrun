/// <summary>
/// Place on a property or field to indicate it is a response reference.
/// </summary>
/// <param name="statusCode">The HTTP status code for the response.</param>
/// <param name="refId">The components/responses id</param>
public interface IOpenApiResponseAttribute
{
    /// <summary>
    /// The HTTP status code for the response.
    /// </summary>
    string StatusCode { get; set; }

    /// <summary>
    /// If true, the schema will be inlined rather than referenced.
    /// </summary>
    bool Inline { get; set; }
}
