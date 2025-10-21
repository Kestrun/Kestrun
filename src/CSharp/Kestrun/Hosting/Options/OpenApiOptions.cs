using Microsoft.OpenApi;

namespace Kestrun.Hosting.Options;

/// <summary>
/// Metadata for OpenAPI documentation related to the route.
/// </summary>
public record OpenAPIMetadata
{
    /// <summary>
    /// Indicates whether OpenAPI documentation is enabled for this route.
    /// </summary>
    public bool Enabled { get; set; }
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
    public string[] Tags { get; set; } = []; // Comma-separated tags
    /// <summary>
    /// External documentation reference for the route.
    /// </summary>
    public OpenApiExternalDocs? ExternalDocs { get; set; }
    /// <summary>
    /// Indicates whether the operation is deprecated in OpenAPI documentation.
    /// </summary>
    public bool Deprecated { get; set; }
    /// <summary>
    /// An alternative server array to service this operation.
    /// If an alternative server object is specified at the Path Item Object or Root level,
    /// it will be overridden by this value.
    /// </summary>
    public IList<OpenApiServer>? Servers { get; set; } = [];

    /// <summary>
    /// A list of parameters that are applicable for this operation.
    /// If a parameter is already defined at the Path Item, the new definition will override it but can never remove it.
    /// The list MUST NOT include duplicated parameters. A unique parameter is defined by a combination of a name and location.
    /// The list can use the Reference Object to link to parameters that are defined at the OpenAPI Object's components/parameters.
    /// </summary>
    public IList<IOpenApiParameter>? Parameters { get; set; } = [];

    /// <summary>
    /// The request body applicable for this operation.
    /// The requestBody is only supported in HTTP methods where the HTTP 1.1 specification RFC7231
    /// has explicitly defined semantics for request bodies.
    /// In other cases where the HTTP spec is vague, requestBody SHALL be ignored by consumers.
    /// </summary>
    public IOpenApiRequestBody? RequestBody { get; set; }

    /// <summary>
    /// REQUIRED. The list of possible responses as they are returned from executing this operation.
    /// </summary>
    public OpenApiResponses? Responses { get; set; } = [];

    /// <summary>
    /// A map of possible out-of band callbacks related to the parent operation.
    /// The key is a unique identifier for the Callback Object.
    /// Each value in the map is a Callback Object that describes a request
    /// that may be initiated by the API provider and the expected responses.
    /// The key value used to identify the callback object is an expression, evaluated at runtime,
    /// that identifies a URL to use for the callback operation.
    /// </summary>
    public IDictionary<string, IOpenApiCallback>? Callbacks { get; set; }

}
