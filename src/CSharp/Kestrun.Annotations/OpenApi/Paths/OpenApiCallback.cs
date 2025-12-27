/// <summary>
/// Attribute to specify OpenAPI path metadata for a route.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class OpenApiCallbackAttribute : KestrunAnnotation, IOpenApiPathAttribute
{
    /// <summary>
    /// The callback expression for the OpenAPI callback object.
    /// </summary>
    public string? Expression { get; init; }
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
    /// Indicates whether the callback should be inlined within the parent OpenAPI document.
    /// </summary>
    public bool Inline { get; set; }
}
