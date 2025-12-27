using Kestrun.Hosting.Options;
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
    private void ApplyCallbackRefAttribute(OpenAPIMetadata metadata, OpenApiCallbackRefAttribute attribute)
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
        if (callback is null)
        {
            throw new InvalidOperationException($"Callback component '{attribute.ReferenceId}' not found for OpenApiCallbackRefAttribute.");
        }
    }
}
