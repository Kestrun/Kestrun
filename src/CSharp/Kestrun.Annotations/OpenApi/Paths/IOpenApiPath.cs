public interface IOpenApiPathAttribute
{
    string? HttpVerb { get; set; }
    /// <summary>
    /// The relative path for the route in OpenAPI documentation.
    /// </summary>
    string? Pattern { get; init; }

    /// <summary>
    /// A brief summary of the route for OpenAPI documentation.
    /// </summary>
    string? Summary { get; set; }
    /// <summary>
    /// A detailed description of the route for OpenAPI documentation.
    /// </summary>
    string? Description { get; set; }

    /// <summary>
    /// The unique operation ID for the route in OpenAPI documentation.
    /// </summary>
    string? OperationId { get; set; }
    /// <summary>
    /// Comma-separated tags for OpenAPI documentation.
    /// </summary>
    string[] Tags { get; set; }

    /// <summary>
    /// Indicates whether the operation is deprecated in OpenAPI documentation.
    /// </summary>
    bool Deprecated { get; set; }

    /// <summary>
    /// The IDs of the OpenAPI document this path belongs to.
    /// if null, it belongs to all documents.
    /// </summary>
    string[]? DocumentId { get; set; }
}
