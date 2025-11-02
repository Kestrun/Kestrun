/// <summary>
/// Attribute to specify OpenAPI path metadata for a route.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class OpenApiPath() : Attribute
{
    public string? HttpVerb { get; set; }
    /// <summary>
    /// The relative path for the route in OpenAPI documentation.
    /// </summary>
    public string? Pattern { get; init; }

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
    public string? Tags { get; set; } // Comma-separated tags

    /// <summary>
    /// Indicates whether the operation is deprecated in OpenAPI documentation.
    /// </summary>
    public bool Deprecated { get; set; }

}
