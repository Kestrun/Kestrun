/// <summary>
/// Attribute to specify OpenAPI path metadata for a route.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class OpenApiPathAttribute : KestrunAnnotation, IOpenApiPathAttribute
{
    /// <inheritdoc/>
    public string? HttpVerb { get; set; }

    /// <inheritdoc/>
    public string? Pattern { get; init; }

    /// <inheritdoc/>
    public string? Summary { get; set; }

    /// <inheritdoc/>
    public string? Description { get; set; }

    /// <inheritdoc/>
    public string? OperationId { get; set; }

    /// <inheritdoc/>
    public string[] Tags { get; set; } = [];

    /// <inheritdoc/>
    public bool Deprecated { get; set; }

    /// <inheritdoc/>
    public string[]? DocumentId { get; set; }

    /// <summary>
    /// The CORS policy name for the route in OpenAPI documentation.
    /// </summary>
    public string? CorsPolicy { get; set; }
}
