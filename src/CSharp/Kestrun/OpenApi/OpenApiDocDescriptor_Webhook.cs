using Kestrun.Hosting.Options;
using Kestrun.Utilities;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper methods for accessing OpenAPI document components.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Populates Document.Webhooks from the registered webhooks using OpenAPI metadata on each webhook.
    /// </summary>
    /// <param name="Metadata"> The dictionary containing webhook patterns, HTTP methods, and their associated OpenAPI metadata.</param>
    private void BuildWebhooks(Dictionary<(string Pattern, HttpVerb Method), OpenAPIPathMetadata> Metadata)
    {
        if (Metadata is null || Metadata.Count == 0)
        {
            return;
        }
        Document.Webhooks = new Dictionary<string, IOpenApiPathItem>();

        var groups = Metadata
            .GroupBy(kvp => kvp.Key.Pattern, StringComparer.Ordinal)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key));

        foreach (var grp in groups)
        {
            ProcessWebhookGroup(grp);
        }
    }

    /// <summary>
    /// Processes a group of webhooks sharing the same pattern to build the corresponding OpenAPI webhook path item.
    /// </summary>
    /// <param name="grp">The group of webhooks sharing the same pattern. </param>
    private void ProcessWebhookGroup(IGrouping<string, KeyValuePair<(string Pattern, HttpVerb Method), OpenAPIPathMetadata>> grp)
    {
        var pattern = grp.Key;
        var webhookPathItem = GetOrCreateWebhookItem(pattern);

        foreach (var kvp in grp)
        {
            if (kvp.Value.DocumentId is not null && !kvp.Value.DocumentId.Contains(DocumentId))
            {
                continue;
            }
            ProcessWebhookOperation(kvp, webhookPathItem);
        }
    }

    /// <summary>
    /// Processes a single webhook operation and adds it to the OpenApiPathItem.
    /// </summary>
    /// <param name="kvp"> The key-value pair representing the webhook pattern, HTTP method, and OpenAPI metadata.</param>
    /// <param name="webhookPathItem"> The OpenApiPathItem to which the operation will be added.</param>
    private void ProcessWebhookOperation(KeyValuePair<(string Pattern, HttpVerb Method), OpenAPIPathMetadata> kvp, OpenApiPathItem webhookPathItem)
    {
        var method = kvp.Key.Method;
        var openapiMetadata = kvp.Value;

        var op = BuildOperationFromMetadata(openapiMetadata);
        webhookPathItem.AddOperation(HttpMethod.Parse(method.ToMethodString()), op);
    }

    /// <summary>
    /// Gets or creates the OpenApiPathItem for the specified webhook pattern.
    /// </summary>
    /// <param name="pattern">The webhook pattern.</param>
    /// <returns>The corresponding OpenApiPathItem.</returns>
    private OpenApiPathItem GetOrCreateWebhookItem(string pattern)
    {
        Document.Webhooks ??= new Dictionary<string, IOpenApiPathItem>(StringComparer.Ordinal);
        if (!Document.Webhooks.TryGetValue(pattern, out var pathInterface) || pathInterface is null)
        {
            pathInterface = new OpenApiPathItem();
            Document.Webhooks[pattern] = pathInterface;
        }
        return (OpenApiPathItem)pathInterface;
    }
}
