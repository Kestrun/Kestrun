using Kestrun.Callback;
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
    /// Applies the OpenApiCallbackRef attribute to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="metadata">The OpenAPI metadata to populate.</param>
    /// <param name="attribute">The OpenApiCallbackRef attribute instance.</param>
    /// <exception cref="InvalidOperationException">Thrown if the referenced callback component is not found.</exception>
    private void ApplyCallbackRefAttribute(OpenAPIPathMetadata metadata, OpenApiCallbackRefAttribute attribute)
    {
        metadata.Callbacks ??= new Dictionary<string, IOpenApiCallback>();

        if (TryGetInline(name: attribute.ReferenceId, kind: OpenApiComponentKind.Callbacks, out OpenApiCallback? callback))
        {
            metadata.Callbacks.Add(attribute.Key, callback!.Clone());
        }
        else if (TryGetComponent(name: attribute.ReferenceId, kind: OpenApiComponentKind.Callbacks, out callback))
        {
            if (attribute.Inline)
            {
                metadata.Callbacks.Add(attribute.Key, callback!.Clone());
            }
            else
            {
                var reference = new OpenApiCallbackReference(attribute.ReferenceId);
                metadata.Callbacks.Add(attribute.Key, reference);
            }
        }
        else if (attribute.Inline)
        {
            throw new InvalidOperationException($"Inline callback component with ID '{attribute.ReferenceId}' not found.");
        }

        if (callback is not null)
        {
            // Compile and store the CallbackPlan for this callback
            metadata.MapOptions.CallbackPlan.AddRange(CallbackPlanCompiler.Compile(callback, attribute.ReferenceId));
        }
    }

    /// <summary>
    /// Populates Document.Callbacks from the registered callbacks using OpenAPI metadata on each callback.
    /// </summary>
    /// <param name="Metadata"> The dictionary containing callback patterns, HTTP methods, and their associated OpenAPI metadata.</param>
    private void BuildCallbacks(Dictionary<(string Pattern, HttpVerb Method), OpenAPIPathMetadata> Metadata)
    {
        if (Metadata is null || Metadata.Count == 0)
        {
            return;
        }

        var groups = Metadata
            .GroupBy(kvp => kvp.Key.Pattern, StringComparer.Ordinal)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key));

        foreach (var grp in groups)
        {
            ProcessCallbacksGroup(grp);
        }
    }
    /// <summary>
    /// Processes a group of callbacks sharing the same pattern to build the corresponding OpenAPI callback item.
    /// </summary>
    /// <param name="grp">The group of callbacks sharing the same pattern. </param>
    private void ProcessCallbacksGroup(IGrouping<string, KeyValuePair<(string Pattern, HttpVerb Method), OpenAPIPathMetadata>> grp)
    {
        var pattern = grp.Key;

        foreach (var kvp in grp)
        {
            if (kvp.Value.DocumentId is not null && !kvp.Value.DocumentId.Contains(DocumentId))
            {
                continue;
            }
            var callbackItem = GetOrCreateCallbackItem(pattern, kvp.Value.Inline);
            ProcessCallbackOperation(kvp, callbackItem);
        }
    }

    /// <summary>
    /// Processes a single callback operation and adds it to the OpenApiCallback.
    /// </summary>
    /// <param name="kvp"> The key-value pair representing the callback pattern, HTTP method, and OpenAPI metadata.</param>
    /// <param name="callbackItem"> The OpenApiCallback to which the operation will be added.</param>
    /// <exception cref="InvalidOperationException"> Thrown when the required Expression property is missing in the OpenAPI metadata.</exception>
    private void ProcessCallbackOperation(KeyValuePair<(string Pattern, HttpVerb Method), OpenAPIPathMetadata> kvp, OpenApiCallback callbackItem)
    {
        callbackItem.PathItems ??= [];
        var method = kvp.Key.Method;
        var openapiMetadata = kvp.Value;
        if (openapiMetadata.Expression is null)
        {
            throw new InvalidOperationException($"Callback OpenAPI metadata for pattern '{kvp.Key.Pattern}' and method '{method}' is missing the required Expression property.");
        }
        // Check if the path item for this expression already exists
        var expr = openapiMetadata.Expression;
        var httpMethod = HttpMethod.Parse(method.ToMethodString());
        // Only add the path item if it doesn't already exist
        if (!callbackItem.PathItems.TryGetValue(expr, out var iPathItem))
        {
            var op = BuildOperationFromMetadata(openapiMetadata);
            var pathItem = new OpenApiPathItem();
            pathItem.AddOperation(httpMethod, op);
            // Add the new path item to the callback
            callbackItem.PathItems.Add(expr, pathItem);
        }
        else
        {
            if (iPathItem is OpenApiPathItem pathItem)
            {
                if (pathItem.Operations is not null && pathItem.Operations.ContainsKey(httpMethod))
                {
                    // Operation for this method already exists; skip adding
                    return;
                }
                var op = BuildOperationFromMetadata(openapiMetadata);
                pathItem.AddOperation(httpMethod, op);
            }
            else
            {
                throw new InvalidOperationException($"Existing path item for expression '{expr.Expression}' is not of type OpenApiPathItem.");
            }
        }
    }

    /// <summary>
    /// Retrieves or creates an OpenApiCallback for the specified pattern.
    /// </summary>
    /// <param name="pattern">The callback pattern.</param>
    /// <param name="inline">Indicates whether the callback is inline.</param>
    /// <returns>The corresponding OpenApiCallback.</returns>
    private OpenApiCallback GetOrCreateCallbackItem(string pattern, bool inline)
    {
        IDictionary<string, IOpenApiCallback> callbacks;
        // Determine whether to use inline components or document components
        if (inline)
        {
            // Use inline components
            InlineComponents.Callbacks ??= new Dictionary<string, IOpenApiCallback>(StringComparer.Ordinal);
            callbacks = InlineComponents.Callbacks;
        }
        else
        {
            // Use document components
            Document.Components ??= new OpenApiComponents();
            Document.Components.Callbacks ??= new Dictionary<string, IOpenApiCallback>(StringComparer.Ordinal);
            callbacks = Document.Components.Callbacks;
        }
        // Retrieve or create the callback item
        if (!callbacks.TryGetValue(pattern, out var pathInterface) || pathInterface is null)
        {
            // Create a new OpenApiCallback if it doesn't exist
            pathInterface = new OpenApiCallback();
            callbacks[pattern] = pathInterface;
        }
        // return the callback item
        return (OpenApiCallback)pathInterface;
    }

    /// <summary>
    /// Applies callback information from metadata to the OpenApiOperation.
    /// </summary>
    /// <param name="op">The OpenApiOperation to modify.</param>
    /// <param name="meta">The OpenAPIPathMetadata containing callback information.</param>
    private static void ApplyCallbacks(OpenApiOperation op, OpenAPIPathMetadata meta)
    {
        if (meta.Callbacks is not null && meta.Callbacks.Count > 0)
        {
            op.Callbacks = new Dictionary<string, IOpenApiCallback>(meta.Callbacks);
        }
    }
}
