namespace Kestrun.Hosting.Options;

/// <summary>
/// Customization options for the OpenAPI metadata generated for the SSE broadcast endpoint.
/// </summary>
public sealed record SignalROptions
{
    /// <summary>
    /// The default path for the SignalR hub endpoint.
    /// </summary>
    public const string DefaultPath = "/signalr/{hub}";

    /// <summary>
    /// The default name for the SignalR hub.
    /// </summary>
    public const string DefaultHubName = "NotificationsHub";

    /// <summary>
    /// The default description for the SignalR hub endpoint.
    /// </summary>
    public const string DefaultDescription = "Establishes a SignalR connection. The server may upgrade to WebSockets or fall back to SSE/Long Polling.";

    /// <summary>
    /// The default summary for the SignalR hub endpoint.
    /// </summary>
    public const string DefaultSummary = "Notifications SignalR Hub";

    /// <summary>
    /// The default OpenAPI tags for the SignalR hub endpoint.
    /// </summary>
    public const string DefaultTag = "SignalR";
    /// <summary>
    /// The path for the SignalR hub endpoint.
    /// </summary>
    public string Path { get; set; } = DefaultPath;

    /// <summary>
    /// Customization options for the OpenAPI metadata generated for the SSE broadcast endpoint.
    /// </summary>
    public string[] DocId { get; set; } = OpenApi.OpenApiDocDescriptor.DefaultDocumentationIds;

    /// <summary>
    /// Gets a default configuration for the SSE broadcast OpenAPI metadata.
    /// </summary>
    public static SignalROptions Default { get; } = new();

    /// <summary>
    /// OpenAPI summary for the endpoint.
    /// </summary>
    public string? Summary { get; init; } = DefaultSummary;

    /// <summary>
    /// OpenAPI description for the endpoint.
    /// </summary>
    public string? Description { get; init; } = DefaultDescription;

    /// <summary>
    /// Name of the SignalR hub.
    /// </summary>
    public string? HubName { get; init; } = DefaultHubName;

    /// <summary>
    /// OpenAPI tags for the endpoint.
    /// </summary>
    public IEnumerable<string> Tags { get; init; } = [DefaultTag];

    /// <summary>
    /// If true, the OpenAPI documentation for this endpoint will be skipped.
    /// </summary>
    public bool SkipOpenApi { get; set; }

    /// <summary>
    /// If true, includes the SignalR negotiate endpoint in OpenAPI documentation.
    /// </summary>
    public bool IncludeNegotiateEndpoint { get; set; } = false;
}
