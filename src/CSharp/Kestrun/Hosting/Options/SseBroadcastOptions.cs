namespace Kestrun.Hosting.Options;

/// <summary>
/// Customization options for the OpenAPI metadata generated for the SSE broadcast endpoint.
/// </summary>
public sealed record SseBroadcastOptions
{

    /// <summary>
    /// The default path for the SSE broadcast endpoint.
    /// </summary>
    public const string DefaultPath = "/sse/broadcast";

    /// <summary>
    /// The default description for the SSE broadcast endpoint.
    /// </summary>
    public const string DefaultDescription = "Opens a Server-Sent Events (text/event-stream) connection that receives server-side broadcast events.";

    /// <summary>
    /// The default tag for the SSE broadcast endpoint.
    /// </summary>
    public const string DefaultTag = "SSE";

    /// <summary>
    /// The default operationId for the SSE broadcast endpoint.
    /// </summary>
    public const string DefaultOperationId = "GetSseBroadcast";

    /// <summary>
    /// The default response description for the SSE broadcast endpoint.
    /// </summary>
    public const string DefaultResponseDescription = "SSE stream (text/event-stream)";

    /// <summary>
    /// The default summary for the SSE broadcast endpoint.
    /// </summary>
    public const string DefaultSummary = "Broadcast SSE stream";

    /// <summary>
    /// The default keep-alive interval in seconds for the SSE connection.
    /// </summary>
    public const int DefaultKeepAliveSeconds = 15;

    /// <summary>
    /// Gets a default configuration for the SSE broadcast OpenAPI metadata.
    /// </summary>
    public static SseBroadcastOptions Default { get; } = new();

    /// <summary>
    /// The path for the SSE broadcast endpoint.
    /// </summary>
    public string Path { get; init; } = DefaultPath;

    /// <summary>
    /// The path for the SSE broadcast endpoint.
    /// </summary>
    public int KeepAliveSeconds { get; init; } = DefaultKeepAliveSeconds;

    /// <summary>
    /// Customization options for the OpenAPI metadata generated for the SSE broadcast endpoint.
    /// </summary>
    public string[] DocId { get; set; } = OpenApi.OpenApiDocDescriptor.DefaultDocumentationIds;

    /// <summary>
    /// OpenAPI operationId for the endpoint.
    /// </summary>
    public string? OperationId { get; init; } = DefaultOperationId;

    /// <summary>
    /// OpenAPI summary for the endpoint.
    /// </summary>
    public string? Summary { get; init; } = DefaultSummary;

    /// <summary>
    /// OpenAPI description for the endpoint.
    /// </summary>
    public string? Description { get; init; } = DefaultDescription;

    /// <summary>
    /// OpenAPI tags for the endpoint.
    /// </summary>
    public IEnumerable<string> Tags { get; init; } = [DefaultTag];

    /// <summary>
    /// Status code used for the SSE response (default: 200).
    /// </summary>
    public string StatusCode { get; init; } = "200";

    /// <summary>
    /// Response description shown in OpenAPI.
    /// </summary>
    public string? ResponseDescription { get; init; } = DefaultResponseDescription;

    /// <summary>
    /// Response content type for the SSE stream (default: text/event-stream).
    /// </summary>
    public string ContentType { get; init; } = "text/event-stream";

    /// <summary>
    /// OpenAPI schema type for the stream payload items (default: string).
    /// </summary>
    public Type ItemSchemaType { get; init; } = typeof(string);

    /// <summary>
    /// Optional OpenAPI schema format for the stream payload (e.g. "uuid", "date-time").
    /// </summary>
    public string? ItemSchemaFormat { get; init; }

    /// <summary>
    /// If true, the OpenAPI documentation for this endpoint will be skipped.
    /// </summary>
    public bool SkipOpenApi { get; set; }

}
