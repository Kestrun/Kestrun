namespace Kestrun.Hosting.Options;

/// <summary>
/// Metadata for OpenAPI documentation related to the route.
/// </summary>
public record OpenAPIMetadata
{
    /// <summary>
    /// A brief summary of the route for OpenAPI documentation.
    /// </summary>
    public string? Summary { get; set; }
    /// <summary>
    /// A detailed description of the route for OpenAPI documentation.
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// The unique operation ID for the route in OpenAPI documentation.
    /// </summary>
    public string? OperationId { get; set; }
    /// <summary>
    /// Comma-separated tags for OpenAPI documentation.
    /// </summary>
    public string[] Tags { get; set; } = []; // Comma-separated tags
    /// <summary>
    /// Group name for OpenAPI documentation.
    /// </summary>
    public string? GroupName { get; set; } // Group name for OpenAPI documentation
}
