namespace Kestrun.Hosting.Options;

/// <summary>
/// Customization options for the OpenAPI metadata generated for the SSE broadcast endpoint.
/// </summary>
public sealed record SseBroadcastOpenApiOptions
{
    /// <summary>
    /// Gets a default configuration for the SSE broadcast OpenAPI metadata.
    /// </summary>
    public static SseBroadcastOpenApiOptions Default { get; } = new();

    /// <summary>
    /// OpenAPI operationId for the endpoint.
    /// </summary>
    public string? OperationId { get; init; } = "GetSseBroadcast";

    /// <summary>
    /// OpenAPI summary for the endpoint.
    /// </summary>
    public string? Summary { get; init; } = "Broadcast SSE stream";

    /// <summary>
    /// OpenAPI description for the endpoint.
    /// </summary>
    public string? Description { get; init; } = "Opens a Server-Sent Events (text/event-stream) connection that receives server-side broadcast events.";

    /// <summary>
    /// OpenAPI tags for the endpoint.
    /// </summary>
    public IEnumerable<string> Tags { get; init; } = ["SSE"];

    /// <summary>
    /// Status code used for the SSE response (default: 200).
    /// </summary>
    public string StatusCode { get; init; } = "200";

    /// <summary>
    /// Response description shown in OpenAPI.
    /// </summary>
    public string? ResponseDescription { get; init; } = "SSE stream (text/event-stream)";

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
}
