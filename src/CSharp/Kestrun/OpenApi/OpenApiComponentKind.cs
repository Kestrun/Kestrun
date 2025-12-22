namespace Kestrun.OpenApi;

/// <summary>
/// Represents the fixed component buckets defined by the OpenAPI 3.2 specification
/// under the <c>components</c> object.
/// </summary>
/// <remarks>
/// This enum mirrors the OpenAPI 3.2 Components Object exactly.
/// It is intended for dispatch, existence checks, retrieval, and removal logic
/// over <see cref="Microsoft.OpenApi.OpenApiComponents"/>.
/// </remarks>
public enum OpenApiComponentKind
{
    /// <summary>
    /// Reusable schema definitions for request and response payloads.
    /// Maps to <c>components/schemas</c>.
    /// </summary>
    Schemas,

    /// <summary>
    /// Reusable response definitions.
    /// Maps to <c>components/responses</c>.
    /// </summary>
    Responses,

    /// <summary>
    /// Reusable parameter definitions (query, header, path, cookie).
    /// Maps to <c>components/parameters</c>.
    /// </summary>
    Parameters,

    /// <summary>
    /// Reusable example definitions.
    /// Maps to <c>components/examples</c>.
    /// </summary>
    Examples,

    /// <summary>
    /// Reusable request body definitions.
    /// Maps to <c>components/requestBodies</c>.
    /// </summary>
    RequestBodies,

    /// <summary>
    /// Reusable header definitions.
    /// Maps to <c>components/headers</c>.
    /// </summary>
    Headers,

    /// <summary>
    /// Reusable security scheme definitions (OAuth2, API key, HTTP auth, etc.).
    /// Maps to <c>components/securitySchemes</c>.
    /// </summary>
    SecuritySchemes,

    /// <summary>
    /// Reusable link definitions describing relationships between operations.
    /// Maps to <c>components/links</c>.
    /// </summary>
    Links,

    /// <summary>
    /// Reusable callback definitions for asynchronous or event-driven APIs.
    /// Maps to <c>components/callbacks</c>.
    /// </summary>
    Callbacks,

    /// <summary>
    /// Reusable path item definitions.
    /// Maps to <c>components/pathItems</c>.
    /// </summary>
    PathItems,

    /// <summary>
    /// Reusable media type definitions (introduced in OpenAPI 3.2).
    /// Maps to <c>components/mediaTypes</c>.
    /// </summary>
    MediaTypes
}
