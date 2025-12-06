using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper methods for accessing OpenAPI document components.
/// </summary>
public partial class OpenApiDocDescriptor
{
    private OpenApiSchema GetSchema(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // Look up schema in components
        return Document.Components?.Schemas is { } schemas
               && schemas.TryGetValue(id, out var p)
               && p is OpenApiSchema op
            ? op
            : throw new InvalidOperationException($"Schema '{id}' not found.");
    }

    private OpenApiParameter GetParameter(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // Look up parameter in components
        return Document.Components?.Parameters is { } parameters
               && parameters.TryGetValue(id, out var p)
               && p is OpenApiParameter op
            ? op
            : throw new InvalidOperationException($"Parameter '{id}' not found.");
    }

    private OpenApiRequestBody GetRequestBody(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // Look up request body in components
        return Document.Components?.RequestBodies is { } requestBodies
               && requestBodies.TryGetValue(id, out var p)
               && p is OpenApiRequestBody op
            ? op
            : throw new InvalidOperationException($"RequestBody '{id}' not found.");
    }

    private OpenApiHeader GetHeader(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // Look up header in components
        return Document.Components?.Headers is { } headers
               && headers.TryGetValue(id, out var p)
               && p is OpenApiHeader op
            ? op
            : throw new InvalidOperationException($"Header '{id}' not found.");
    }

    private OpenApiResponse GetResponse(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // Look up response in components
        return Document.Components?.Responses is { } responses
               && responses.TryGetValue(id, out var p)
               && p is OpenApiResponse op
            ? op
            : throw new InvalidOperationException($"Response '{id}' not found.");
    }

    private bool ComponentSchemasExists(string id) =>
        Document.Components?.Schemas?.ContainsKey(id) == true;

    private bool ComponentRequestBodiesExists(string id) =>
        Document.Components?.RequestBodies?.ContainsKey(id) == true;

    private bool ComponentResponsesExists(string id) =>
        Document.Components?.Responses?.ContainsKey(id) == true;
    private bool ComponentParametersExists(string id) =>
        Document.Components?.Parameters?.ContainsKey(id) == true;

    private bool ComponentExamplesExists(string id) =>
            Document.Components?.Examples?.ContainsKey(id) == true;

    private bool ComponentHeadersExists(string id) =>
            Document.Components?.Headers?.ContainsKey(id) == true;
    private bool ComponentCallbacksExists(string id) =>
            Document.Components?.Callbacks?.ContainsKey(id) == true;

    private bool ComponentLinksExists(string id) =>
            Document.Components?.Links?.ContainsKey(id) == true;
    private bool ComponentPathItemsExists(string id) =>
            Document.Components?.PathItems?.ContainsKey(id) == true;
}
