using System.ComponentModel;
using Kestrun.Mcp;
using ModelContextProtocol.Server;

namespace Kestrun.Mcp.ServerHost;

/// <summary>
/// MCP tool surface for inspecting a live Kestrun host.
/// </summary>
[McpServerToolType]
internal sealed class KestrunMcpTools(
    KestrunMcpRuntime runtime,
    IKestrunRouteInspector routeInspector,
    IKestrunOpenApiProvider openApiProvider,
    IKestrunRuntimeInspector runtimeInspector,
    IKestrunRequestValidator requestValidator,
    IKestrunRequestInvoker requestInvoker)
{
    private readonly KestrunMcpRuntime _runtime = runtime;
    private readonly IKestrunRouteInspector _routeInspector = routeInspector;
    private readonly IKestrunOpenApiProvider _openApiProvider = openApiProvider;
    private readonly IKestrunRuntimeInspector _runtimeInspector = runtimeInspector;
    private readonly IKestrunRequestValidator _requestValidator = requestValidator;
    private readonly IKestrunRequestInvoker _requestInvoker = requestInvoker;

    /// <summary>
    /// Lists registered Kestrun routes.
    /// </summary>
    /// <returns>Registered route summaries.</returns>
    [McpServerTool(Name = "kestrun.list_routes", UseStructuredContent = true), Description("Return all registered Kestrun routes with route, OpenAPI, and handler metadata.")]
    public async Task<IReadOnlyList<KestrunRouteSummary>> ListRoutes(CancellationToken cancellationToken)
    {
        var host = await _runtime.WaitForHostAsync(cancellationToken).ConfigureAwait(false);
        return _routeInspector.ListRoutes(host);
    }

    /// <summary>
    /// Returns route metadata for one selected route.
    /// </summary>
    /// <param name="pattern">Route pattern to inspect.</param>
    /// <param name="operationId">OpenAPI operation identifier to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed route metadata.</returns>
    [McpServerTool(Name = "kestrun.get_route", UseStructuredContent = true), Description("Return detailed metadata for a single Kestrun route selected by pattern and/or operation id.")]
    public async Task<KestrunRouteDetail> GetRoute(
        [Description("Route pattern to inspect, for example /hello or /api/items/{id}.")] string? pattern = null,
        [Description("OpenAPI operation id to inspect.")] string? operationId = null,
        CancellationToken cancellationToken = default)
    {
        var host = await _runtime.WaitForHostAsync(cancellationToken).ConfigureAwait(false);
        return _routeInspector.GetRoute(host, pattern, operationId);
    }

    /// <summary>
    /// Returns a structured OpenAPI document.
    /// </summary>
    /// <param name="version">Requested OpenAPI version such as 3.0, 3.1, or 3.2.</param>
    /// <param name="documentId">Requested document id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The structured OpenAPI document.</returns>
    [McpServerTool(Name = "kestrun.get_openapi", UseStructuredContent = true), Description("Return the generated Kestrun OpenAPI document as structured JSON.")]
    public async Task<KestrunOpenApiDocumentResult> GetOpenApi(
        [Description("Requested OpenAPI version such as 3.0, 3.1, or 3.2.")] string? version = null,
        [Description("OpenAPI document id. Omit to use the default document.")] string? documentId = null,
        CancellationToken cancellationToken = default)
    {
        var host = await _runtime.WaitForHostAsync(cancellationToken).ConfigureAwait(false);
        return _openApiProvider.GetOpenApi(host, documentId, version);
    }

    /// <summary>
    /// Returns a safe runtime summary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The runtime summary.</returns>
    [McpServerTool(Name = "kestrun.inspect_runtime", UseStructuredContent = true), Description("Return safe Kestrun runtime information such as status, uptime, listeners, and non-sensitive configuration.")]
    public async Task<KestrunRuntimeInspectionResult> InspectRuntime(CancellationToken cancellationToken)
    {
        var host = await _runtime.WaitForHostAsync(cancellationToken).ConfigureAwait(false);
        return _runtimeInspector.Inspect(host);
    }

    /// <summary>
    /// Validates a proposed request against the selected Kestrun host.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Request path.</param>
    /// <param name="headers">Optional headers.</param>
    /// <param name="query">Optional query values.</param>
    /// <param name="body">Optional request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    [McpServerTool(Name = "kestrun.validate_request", UseStructuredContent = true), Description("Validate whether a proposed request would satisfy Kestrun route matching, content-type, and Accept constraints.")]
    public async Task<KestrunRequestValidationResult> ValidateRequest(
        [Description("HTTP method to validate.")] string method,
        [Description("Route path to validate.")] string path,
        [Description("Optional request headers.")] IDictionary<string, string>? headers = null,
        [Description("Optional query-string values.")] IDictionary<string, string>? query = null,
        [Description("Optional request body.")] object? body = null,
        CancellationToken cancellationToken = default)
    {
        var host = await _runtime.WaitForHostAsync(cancellationToken).ConfigureAwait(false);
        return _requestValidator.Validate(host, new KestrunRequestInput
        {
            Method = method,
            Path = path,
            Headers = headers,
            Query = query,
            Body = body
        });
    }

    /// <summary>
    /// Invokes a route through the normal HTTP pipeline.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Request path.</param>
    /// <param name="headers">Optional headers.</param>
    /// <param name="query">Optional query values.</param>
    /// <param name="body">Optional request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    [McpServerTool(Name = "kestrun.invoke_route", UseStructuredContent = true), Description("Safely invoke a Kestrun route through the normal HTTP pipeline when invocation is explicitly enabled.")]
    public async Task<KestrunRouteInvokeResult> InvokeRoute(
        [Description("HTTP method to invoke.")] string method,
        [Description("Route path to invoke.")] string path,
        [Description("Optional request headers.")] IDictionary<string, string>? headers = null,
        [Description("Optional query-string values.")] IDictionary<string, string>? query = null,
        [Description("Optional request body.")] object? body = null,
        CancellationToken cancellationToken = default)
    {
        var host = await _runtime.WaitForHostAsync(cancellationToken).ConfigureAwait(false);
        return await _requestInvoker.InvokeAsync(host, new KestrunRequestInput
        {
            Method = method,
            Path = path,
            Headers = headers,
            Query = query,
            Body = body
        }, cancellationToken).ConfigureAwait(false);
    }
}
