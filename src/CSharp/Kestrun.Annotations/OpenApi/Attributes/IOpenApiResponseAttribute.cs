/// <summary>
/// Interface for OpenAPI Response attributes.
/// </summary>
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
