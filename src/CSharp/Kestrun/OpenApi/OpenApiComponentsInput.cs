// File: OpenApiComponentsInput.cs
// Target: net8.0
// Ref: <PackageReference Include="Microsoft.OpenApi" Version="2.3.5" />

using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Optional inputs for OpenAPI Components. Any property left null is simply ignored.
/// </summary>
public sealed class OpenApiComponentsInput
{
    /// <summary>
    /// Responses available for reuse in the specification.
    /// </summary>
    public IDictionary<string, IOpenApiResponse>? Responses { get; set; }
    /// <summary>
    /// Parameters available for reuse in the specification.
    /// </summary>
    public IDictionary<string, IOpenApiParameter>? Parameters { get; set; }
    /// <summary>
    /// Examples available for reuse in the specification.
    /// </summary>
    public IDictionary<string, IOpenApiExample>? Examples { get; set; }        // v2 uses JsonNode for Examples
    /// <summary>
    /// Request bodies available for reuse in the specification.
    /// </summary>
    public IDictionary<string, IOpenApiRequestBody>? RequestBodies { get; set; }
    /// <summary>
    /// Headers available for reuse in the specification.
    /// </summary>
    public IDictionary<string, IOpenApiHeader>? Headers { get; set; }
    /// <summary>
    /// Security schemes available for reuse in the specification.
    /// </summary>
    public IDictionary<string, IOpenApiSecurityScheme>? SecuritySchemes { get; set; }
    /// <summary>
    /// Links available for reuse in the specification.
    /// </summary>
    public IDictionary<string, IOpenApiLink>? Links { get; set; }
    /// <summary>
    /// Callbacks available for reuse in the specification.
    /// </summary>
    public IDictionary<string, IOpenApiCallback>? Callbacks { get; set; }
    /// <summary>
    /// Path items available for reuse in the specification.
    /// </summary>
    public IDictionary<string, IOpenApiPathItem>? PathItems { get; set; }
    /// <summary>
    /// Extensions available for reuse in the specification.
    /// </summary>
    public IDictionary<string, IOpenApiExtension>? Extensions { get; set; }
    // Schemas are produced by your generator from Types, so not included here.
}
