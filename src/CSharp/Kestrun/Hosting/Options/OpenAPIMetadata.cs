using Kestrun.OpenApi;
using Microsoft.OpenApi;

namespace Kestrun.Hosting.Options;

/// <summary>
/// Metadata for OpenAPI documentation related to the route.
/// </summary>
public record OpenAPIMetadata : OpenAPICommonMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAPIMetadata"/> class with the specified pattern.
    /// </summary>
    /// <param name="pattern">The route pattern.</param>
    public OpenAPIMetadata(string pattern)
        : base(pattern)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAPIMetadata"/> class.
    /// </summary>
    public OpenAPIMetadata()
    {
    }
    /// <summary>
    /// The unique operation ID for the route in OpenAPI documentation.
    /// </summary>
    public string? OperationId { get; set; }
    /// <summary>
    /// Comma-separated tags for OpenAPI documentation.
    /// </summary>
    public List<string> Tags { get; set; } = []; // Comma-separated tags
    /// <summary>
    /// External documentation reference for the route.
    /// </summary>
    public OpenApiExternalDocs? ExternalDocs { get; set; }
    /// <summary>
    /// Indicates whether the operation is deprecated in OpenAPI documentation.
    /// </summary>
    public bool Deprecated { get; set; }

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

    /// <summary>
    /// A declaration of which security mechanisms can be used for this operation.
    /// The list of values includes alternative security requirement objects that can be used.
    /// </summary>
    public List<Dictionary<string, List<string>>>? SecuritySchemes { get; set; }

    /// <summary>
    /// The kind of OpenAPI path-like object represented by this metadata.
    /// </summary>
    public OpenApiPathLikeKind? PathLikeKind { get; set; }

    /// <summary>
    /// Indicates whether this metadata represents an OpenAPI webhook.
    /// </summary>
    public bool IsOpenApiWebhook => PathLikeKind == OpenApiPathLikeKind.Webhook;

    /// <summary>
    /// Indicates whether this metadata represents an OpenAPI path.
    /// </summary>
    public bool IsOpenApiPath => PathLikeKind == OpenApiPathLikeKind.Path;

    /// <summary>
    /// Indicates whether this metadata represents an OpenAPI callback.
    /// </summary>
    public bool IsOpenApiCallback => PathLikeKind == OpenApiPathLikeKind.Callback;

    /// <summary>
    /// The IDs of the OpenAPI document this metadata belongs to.
    /// If null, it belongs to all documents.
    /// </summary>
    public string[]? DocumentId { get; set; }

    /// <summary>
    /// The callback expression for the OpenAPI callback object.
    /// </summary>
    public RuntimeExpression? Expression { get; set; }
}
