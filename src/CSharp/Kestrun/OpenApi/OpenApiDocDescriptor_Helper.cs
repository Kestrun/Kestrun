using System.Collections;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper methods for accessing OpenAPI document components.
/// </summary>
public partial class OpenApiDocDescriptor
{
    private IOpenApiSchema GetSchema(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // Look up schema in components
        return Document.Components?.Schemas is { } schemas
               && schemas.TryGetValue(id, out var p)
               && p is IOpenApiSchema op
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

    /// <summary>
    /// Normalizes a raw extensions dictionary into OpenAPI extensions.
    /// </summary>
    /// <param name="extensions">The raw extensions dictionary to normalize.</param>
    /// <returns>A normalized dictionary of OpenAPI extensions, or null if no valid extensions exist.</returns>
    private static Dictionary<string, IOpenApiExtension>? NormalizeExtensions(IDictionary? extensions)
    {
        if (extensions is null || extensions.Count == 0)
        {
            return null;
        }

        Dictionary<string, IOpenApiExtension>? result = null;

        foreach (DictionaryEntry entry in extensions)
        {
            var rawKey = entry.Key?.ToString();
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                continue;
            }

            // Enforce OpenAPI extension naming
            var key = rawKey.StartsWith("x-", StringComparison.OrdinalIgnoreCase)
                ? rawKey
                : "x-" + rawKey;

            var node = OpenApiJsonNodeFactory.ToNode(entry.Value);
            if (node is null)
            {
                continue;
            }

            result ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
            result[key] = new JsonNodeExtension(node);
        }

        return result;
    }
}
