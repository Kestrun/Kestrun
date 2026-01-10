namespace Kestrun.Hosting.Options;

/// <summary>
/// Customization options for the OpenAPI metadata generated for the SSE broadcast endpoint.
/// </summary>
public sealed record SignalROptions
{

    /// <summary>
    /// The path for the SignalR hub endpoint.
    /// </summary>
    public string Path { get; set; } = "/signalr/{hub}";

    /// <summary>
    /// Customization options for the OpenAPI metadata generated for the SSE broadcast endpoint.
    /// </summary>
    public string DocId { get; set; } = OpenApi.OpenApiDocDescriptor.DefaultDocumentationId;

    /// <summary>
    /// Gets a default configuration for the SSE broadcast OpenAPI metadata.
    /// </summary>
    public static SignalROptions Default { get; } = new();


    /// <summary>
    /// OpenAPI summary for the endpoint.
    /// </summary>
    public string? Summary { get; init; } = "Notifications SignalR Hub";

    /// <summary>
    /// OpenAPI description for the endpoint.
    /// </summary>
    public string? Description { get; init; } =
    "Establishes a SignalR connection. The server may upgrade to WebSockets or fall back to SSE/Long Polling.";

    /// <summary>
    /// Name of the SignalR hub.
    /// </summary>
    public string? HubName { get; init; } = "NotificationsHub";

    /// <summary>
    /// OpenAPI tags for the endpoint.
    /// </summary>
    public IEnumerable<string> Tags { get; init; } = ["SignalR"];

    /// <summary>
    /// If true, the OpenAPI documentation for this endpoint will be skipped.
    /// </summary>
    public bool SkipOpenApi { get; set; }

    /// <summary>
    /// If true, includes the SignalR negotiate endpoint in OpenAPI documentation.
    /// </summary>
    public bool IncludeNegotiateEndpoint { get; set; } = false;
}
