using System.Text.Json.Nodes;
using Kestrun.Hosting;
namespace Kestrun.Mcp;

/// <summary>
/// Structured error information returned by Kestrun MCP services.
/// </summary>
/// <param name="Code">Stable error code.</param>
/// <param name="Message">Human-readable explanation.</param>
/// <param name="Details">Optional machine-readable details.</param>
public sealed record KestrunMcpError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?>? Details = null);

/// <summary>
/// Summarized route metadata for MCP discovery operations.
/// </summary>
public sealed record KestrunRouteSummary
{
    /// <summary>Route pattern.</summary>
    public required string Pattern { get; init; }
    /// <summary>HTTP methods.</summary>
    public required IReadOnlyList<string> Verbs { get; init; }
    /// <summary>OpenAPI tags.</summary>
    public required IReadOnlyList<string> Tags { get; init; }
    /// <summary>OpenAPI summary.</summary>
    public string? Summary { get; init; }
    /// <summary>OpenAPI description.</summary>
    public string? Description { get; init; }
    /// <summary>Supported request content types.</summary>
    public required IReadOnlyList<string> RequestContentTypes { get; init; }
    /// <summary>Supported response content types.</summary>
    public required IReadOnlyList<string> ResponseContentTypes { get; init; }
    /// <summary>Bound handler name when available.</summary>
    public string? HandlerName { get; init; }
    /// <summary>Best-effort script/runtime language.</summary>
    public string? HandlerLanguage { get; init; }
    /// <summary>OpenAPI operation identifier when available.</summary>
    public string? OperationId { get; init; }
}

/// <summary>
/// Detailed route metadata for a single route selection.
/// </summary>
public sealed record KestrunRouteDetail
{
    /// <summary>Route summary payload.</summary>
    public required KestrunRouteSummary Route { get; init; }
    /// <summary>Request schema keyed by content type when available.</summary>
    public required IReadOnlyDictionary<string, JsonNode?> RequestSchemas { get; init; }
    /// <summary>Response metadata keyed by status code.</summary>
    public required IReadOnlyDictionary<string, KestrunRouteResponseSchema> Responses { get; init; }
    /// <summary>Route lookup error when selection fails.</summary>
    public KestrunMcpError? Error { get; init; }
}

/// <summary>
/// Response metadata extracted from OpenAPI.
/// </summary>
public sealed record KestrunRouteResponseSchema
{
    /// <summary>Response description.</summary>
    public string? Description { get; init; }
    /// <summary>Response schemas keyed by content type.</summary>
    public required IReadOnlyDictionary<string, JsonNode?> Content { get; init; }
}

/// <summary>
/// Structured OpenAPI document payload.
/// </summary>
public sealed record KestrunOpenApiDocumentResult
{
    /// <summary>Requested document id.</summary>
    public required string DocumentId { get; init; }
    /// <summary>Resolved OpenAPI version string.</summary>
    public required string Version { get; init; }
    /// <summary>Structured document payload.</summary>
    public JsonNode? Document { get; init; }
    /// <summary>Lookup error when retrieval fails.</summary>
    public KestrunMcpError? Error { get; init; }
}

/// <summary>
/// Listener metadata exposed through runtime inspection.
/// </summary>
public sealed record KestrunRuntimeListener
{
    /// <summary>Listener URL.</summary>
    public required string Url { get; init; }
    /// <summary>Transport protocols.</summary>
    public required string Protocols { get; init; }
    /// <summary>Whether HTTPS is enabled.</summary>
    public bool UseHttps { get; init; }
}

/// <summary>
/// Safe runtime inspection payload.
/// </summary>
public sealed record KestrunRuntimeInspectionResult
{
    /// <summary>Application name.</summary>
    public required string ApplicationName { get; init; }
    /// <summary>Host status.</summary>
    public required string Status { get; init; }
    /// <summary>Environment name.</summary>
    public required string Environment { get; init; }
    /// <summary>Start timestamp in UTC.</summary>
    public DateTime? StartTimeUtc { get; init; }
    /// <summary>Stop timestamp in UTC.</summary>
    public DateTime? StopTimeUtc { get; init; }
    /// <summary>Current uptime when available.</summary>
    public TimeSpan? Uptime { get; init; }
    /// <summary>Known listeners.</summary>
    public required IReadOnlyList<KestrunRuntimeListener> Listeners { get; init; }
    /// <summary>Known route count.</summary>
    public int RouteCount { get; init; }
    /// <summary>Selected safe configuration values.</summary>
    public required IReadOnlyDictionary<string, object?> Configuration { get; init; }
}

/// <summary>
/// Proposed request payload used by validation and invocation tools.
/// </summary>
public sealed record KestrunRequestInput
{
    /// <summary>HTTP method.</summary>
    public string Method { get; init; } = "GET";
    /// <summary>Request path.</summary>
    public string Path { get; init; } = "/";
    /// <summary>Request headers.</summary>
    public IDictionary<string, string>? Headers { get; init; }
    /// <summary>Request query values.</summary>
    public IDictionary<string, string>? Query { get; init; }
    /// <summary>Request body.</summary>
    public object? Body { get; init; }
}

/// <summary>
/// Request validation result.
/// </summary>
public sealed record KestrunRequestValidationResult
{
    /// <summary>Whether the request would likely succeed.</summary>
    public bool IsValid { get; init; }
    /// <summary>Likely resulting status code.</summary>
    public int StatusCode { get; init; }
    /// <summary>Best-effort explanation.</summary>
    public required string Message { get; init; }
    /// <summary>Matched route summary when available.</summary>
    public KestrunRouteSummary? Route { get; init; }
    /// <summary>Validation error details.</summary>
    public KestrunMcpError? Error { get; init; }
}

/// <summary>
/// Route invocation result returned by the safe invoker.
/// </summary>
public sealed record KestrunRouteInvokeResult
{
    /// <summary>Response status code.</summary>
    public int StatusCode { get; init; }
    /// <summary>Response content type.</summary>
    public string? ContentType { get; init; }
    /// <summary>Response headers.</summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
    /// <summary>Response body text.</summary>
    public string? Body { get; init; }
    /// <summary>Invocation error details.</summary>
    public KestrunMcpError? Error { get; init; }
}

/// <summary>
/// Configures safety boundaries for the MCP request invoker.
/// </summary>
public sealed record KestrunRequestInvokerOptions
{
    /// <summary>Whether route invocation is enabled.</summary>
    public bool EnableInvocation { get; init; }
    /// <summary>Allowlisted route patterns for invocation.</summary>
    public IReadOnlyList<string> AllowedPathPatterns { get; init; } = [];
    /// <summary>Headers to redact in tool output.</summary>
    public IReadOnlySet<string> RedactedHeaders { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "Cookie",
            "Set-Cookie",
            "Proxy-Authorization",
            "X-Api-Key",
            "Api-Key"
        };
}

/// <summary>
/// Route inspection contract used by MCP tool handlers.
/// </summary>
public interface IKestrunRouteInspector
{
    /// <summary>Returns all registered routes.</summary>
    IReadOnlyList<KestrunRouteSummary> ListRoutes(KestrunHost host);
    /// <summary>Returns one selected route.</summary>
    KestrunRouteDetail GetRoute(KestrunHost host, string? pattern = null, string? operationId = null);
}

/// <summary>
/// OpenAPI retrieval contract used by MCP tool handlers.
/// </summary>
public interface IKestrunOpenApiProvider
{
    /// <summary>Returns the requested OpenAPI document.</summary>
    KestrunOpenApiDocumentResult GetOpenApi(KestrunHost host, string? documentId = null, string? version = null);
}

/// <summary>
/// Runtime inspection contract used by MCP tool handlers.
/// </summary>
public interface IKestrunRuntimeInspector
{
    /// <summary>Returns a safe runtime summary.</summary>
    KestrunRuntimeInspectionResult Inspect(KestrunHost host);
}

/// <summary>
/// Request validation contract used by MCP tool handlers.
/// </summary>
public interface IKestrunRequestValidator
{
    /// <summary>Validates a proposed request without executing the route handler.</summary>
    KestrunRequestValidationResult Validate(KestrunHost host, KestrunRequestInput input);
}

/// <summary>
/// Request invocation contract used by MCP tool handlers.
/// </summary>
public interface IKestrunRequestInvoker
{
    /// <summary>Invokes a route through the normal HTTP pipeline.</summary>
    Task<KestrunRouteInvokeResult> InvokeAsync(KestrunHost host, KestrunRequestInput input, CancellationToken cancellationToken = default);
}
