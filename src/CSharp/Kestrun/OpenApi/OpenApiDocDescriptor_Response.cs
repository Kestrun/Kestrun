using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Gets the name override from an attribute, if present.
    /// </summary>
    /// <param name="attr">The attribute to inspect.</param>
    /// <returns>The name override, if present; otherwise, null.</returns>
    private static string? GetKeyOverride(object attr)
    {
        var t = attr.GetType();
        return t.GetProperty("Key")?.GetValue(attr) as string;
    }

    /// <summary>
    /// Creates or modifies an OpenApiResponse based on the provided attribute.
    /// </summary>
    /// <param name="attr"> The attribute to apply.</param>
    /// <param name="response"> The OpenApiResponse to modify.</param>
    /// <param name="iSchema"> An optional schema to apply.</param>
    /// <returns>True if the response was modified; otherwise, false.</returns>
    private bool CreateResponseFromAttribute(object attr, OpenApiResponse? response, IOpenApiSchema? iSchema = null)
    {
        ArgumentNullException.ThrowIfNull(attr);
        ArgumentNullException.ThrowIfNull(response);

        return attr switch
        {
            OpenApiResponseAttribute resp => ApplyResponseAttribute(resp, response, iSchema),
            OpenApiResponseHeaderRefAttribute href => ApplyHeaderRefAttribute(href, response),
            OpenApiResponseHeaderAttribute head => ApplyHeaderAttribute(head, response),
            OpenApiResponseLinkRefAttribute lref => ApplyLinkRefAttribute(lref, response),
            OpenApiExampleRefAttribute exRef => ApplyExampleRefAttribute(exRef, response),
            OpenApiResponseExampleRefAttribute exRef => ApplyExampleRefAttribute(exRef, response),
            _ => false
        };
    }
    // --- local helpers -------------------------------------------------------

    /// <summary>
    /// Applies an OpenApiResponseAttribute to an OpenApiResponse.
    /// </summary>
    /// <param name="resp">The OpenApiResponseAttribute to apply.</param>
    /// <param name="response">The OpenApiResponse to modify.</param>
    /// <param name="schema">An optional schema to apply.</param>
    /// <returns>True if the response was modified; otherwise, false.</returns>
    private bool ApplyResponseAttribute(OpenApiResponseAttribute resp, OpenApiResponse response, IOpenApiSchema? schema)
    {
        ApplyDescription(resp, response);
        schema = ResolveResponseSchema(resp, schema);
        ApplySchemaToContentTypes(resp, response, schema);
        return true;
    }

    private static void ApplyDescription(OpenApiResponseAttribute resp, OpenApiResponse response)
    {
        if (!string.IsNullOrEmpty(resp.Description))
        {
            response.Description = resp.Description;
        }
    }

    private IOpenApiSchema? ResolveResponseSchema(OpenApiResponseAttribute resp, IOpenApiSchema? propertySchema)
    {
        // 1) Type-based schema
        if (resp.Schema is not null)
        {
            return InferPrimitiveSchema(resp.Schema, inline: resp.Inline);
        }
        if (resp.SchemaItem is not null)
        {
            return InferPrimitiveSchema(resp.SchemaItem, inline: resp.Inline);
        }

        // 4) Fallback to existing property schema (primitive/concrete)
        return propertySchema;
    }


    private void ApplySchemaToContentTypes(OpenApiResponseAttribute resp, OpenApiResponse response, IOpenApiSchema? schema)
    {
        if (schema is not null && resp.ContentType is { Length: > 0 })
        {
            foreach (var ct in resp.ContentType)
            {
                var media = GetOrAddMediaType(response, ct);
                if (media is OpenApiMediaType mediaType)
                {
                    if (resp.SchemaItem != null)
                    {
                        mediaType.ItemSchema = schema;
                    }
                    else
                    {
                        mediaType.Schema = schema;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Applies a header reference attribute to an OpenAPI response.
    /// </summary>
    /// <param name="href">The header reference attribute.</param>
    /// <param name="response">The OpenAPI response to modify.</param>
    /// <returns>True if the header reference was applied; otherwise, false.</returns>
    private bool ApplyHeaderRefAttribute(OpenApiResponseHeaderRefAttribute href, OpenApiResponse response)
    {
        // ensure headers dictionary
        response.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
        // create header reference
        return TryAddHeader(response.Headers, href);
    }

    private bool ApplyHeaderAttribute(OpenApiResponseHeaderAttribute href, OpenApiResponse response)
    {
        // ensure headers dictionary
        response.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
        // create header from attribute
        var header = NewOpenApiHeader(
            description: href.Description,
            required: href.Required,
            deprecated: href.Deprecated,
            allowEmptyValue: href.AllowEmptyValue,
            style: href.Style != null ? ((OaParameterStyle)href.Style).ToOpenApi() : null,
            explode: href.Explode,
            allowReserved: href.AllowReserved,
            example: href.Example,
            examples: null,
            schema: href.Schema,
            content: null
        );
        // add header to response
        return response.Headers.TryAdd(href.Key, header);
    }

    /// <summary>
    /// Applies an example reference attribute to an OpenAPI response.
    /// </summary>
    /// <param name="exRef">The example reference attribute.</param>
    /// <param name="response">The OpenAPI response to modify.</param>
    /// <returns>True if the example reference was applied; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the example reference cannot be embedded because it was not found in components or is not an OpenApiExample.</exception>
    private bool ApplyExampleRefAttribute(OpenApiExampleRefAttribute exRef, OpenApiResponse response)
    {
        foreach (var contentType in ResolveExampleTargets(exRef, response))
        {
            if (GetOrAddMediaType(response, contentType) is not OpenApiMediaType media)
            {
                continue;
            }

            media.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
            media.Examples[exRef.Key] = exRef.Inline
                ? CloneExampleOrThrow(exRef.ReferenceId)
                : new OpenApiExampleReference(exRef.ReferenceId);
        }
        return true;
    }

    private bool ApplyExampleRefAttribute(OpenApiResponseExampleRefAttribute attribute, OpenApiResponse response)
    {
        foreach (var contentType in ResolveExampleTargets(attribute, response))
        {
            if (GetOrAddMediaType(response, contentType) is not OpenApiMediaType media)
            {
                continue;
            }

            media.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
            // Clone or reference the example
            _ = TryAddExample(media.Examples, attribute);
        }
        return true;
    }

    private static IEnumerable<string> ResolveExampleTargets(OpenApiExampleRefAttribute exRef, OpenApiResponse response)
    {
        var targets = exRef.ContentType is null
            ? (IEnumerable<string>)(response.Content?.Keys ?? Array.Empty<string>())
            : exRef.ContentType;

        return targets.Any() ? targets : ["application/json"];
    }

    private static IEnumerable<string> ResolveExampleTargets(OpenApiResponseExampleRefAttribute exRef, OpenApiResponse response)
    {
        var targets = exRef.ContentType is null
            ? (IEnumerable<string>)(response.Content?.Keys ?? Array.Empty<string>())
            : exRef.ContentType;

        return targets.Any() ? targets : ["application/json"];
    }

    /// <summary>
    /// Gets or adds a media type to the response for the specified content type.
    /// </summary>
    /// <param name="resp">The OpenAPI response object.</param>
    /// <param name="contentType">The content type for the media type.</param>
    /// <returns>The media type associated with the specified content type.</returns>
    private static IOpenApiMediaType GetOrAddMediaType(OpenApiResponse resp, string contentType)
    {
        resp.Content ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);
        if (!resp.Content.TryGetValue(contentType, out var media))
        {
            media = resp.Content[contentType] = new OpenApiMediaType();
        }

        return media;
    }

    private OpenApiSchema CloneSchemaOrThrow(string refId)
    {
        if (Document.Components?.Schemas is { } schemas &&
            schemas.TryGetValue(refId, out var schema))
        {
            // your existing clone semantics
            return (OpenApiSchema)schema.Clone();
        }

        throw new InvalidOperationException(
            $"Schema reference '{refId}' cannot be embedded because it was not found in components.");
    }

    private void ProcessResponseExampleRef(string name, OpenApiResponseExampleRefAttribute attribute)
    {
        if (attribute.StatusCode != "default")
        {
            throw new InvalidOperationException("Response example references cannot have a status code.");
        }
        if (attribute.Key is null)
        {
            throw new InvalidOperationException("Response example attributes must have a Key specified to define the example name under response.examples.");
        }
        if (!TryGetResponseItem(name, out var response))
        {
            throw new InvalidOperationException($"response '{name}' not found when trying to add to response.");
        }
        _ = CreateResponseFromAttribute(attribute, response);
    }

    private void ProcessResponseLinkRef(string name, OpenApiResponseLinkRefAttribute attribute)
    {
        if (attribute.StatusCode != "default")
        {
            throw new InvalidOperationException("Response link references cannot have a status code.");
        }
        if (attribute.Key is null)
        {
            throw new InvalidOperationException("Response link attributes must have a Key specified to define the link name under response.links.");
        }
        if (!TryGetResponseItem(name, out var response))
        {
            throw new InvalidOperationException($"response '{name}' not found when trying to add to response.");
        }
        _ = CreateResponseFromAttribute(attribute, response);
    }

    private void ProcessResponseHeaderRef(string name, OpenApiResponseHeaderRefAttribute attribute)
    {
        if (attribute.StatusCode != "default")
        {
            throw new InvalidOperationException("Response header references cannot have a status code.");
        }
        if (attribute.Key is null)
        {
            throw new InvalidOperationException("Response header attributes must have a Key specified to define the header name under response.headers.");
        }
        if (!TryGetResponseItem(name, out var response))
        {
            throw new InvalidOperationException($"response '{name}' not found when trying to add to response.");
        }
        _ = CreateResponseFromAttribute(attribute, response);
    }

    private void ProcessResponseComponent(
      OpenApiComponentAnnotationScanner.AnnotatedVariable variable,
      OpenApiResponseComponentAttribute responseDescriptor)
    {
        var response = GetOrCreateResponseItem(variable.Name, responseDescriptor.Inline);

        ApplyResponseCommonFields(response, responseDescriptor);

        TryApplyVariableTypeSchema(response, variable, responseDescriptor);
    }

    #region Response Item Helpers

    private OpenApiResponse GetOrCreateResponseItem(string responseName, bool inline)
    {
        IDictionary<string, IOpenApiResponse> responses;
        // Determine whether to use inline components or document components
        if (inline)
        {
            // Use inline components
            InlineComponents.Responses ??= new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal);
            responses = InlineComponents.Responses;
        }
        else
        {
            // Use document components
            Document.Components ??= new OpenApiComponents();
            Document.Components.Responses ??= new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal);
            responses = Document.Components.Responses;
        }
        // Retrieve or create the response item
        if (!responses.TryGetValue(responseName, out var responseInterface) || responseInterface is null)
        {
            // Create a new OpenApiResponse if it doesn't exist
            responseInterface = new OpenApiResponse();
            responses[responseName] = responseInterface;
        }
        // return the response item
        return (OpenApiResponse)responseInterface;
    }

    /// <summary>
    /// Tries to get a response item by name from either inline or document components.
    /// </summary>
    /// <param name="responseName"> The name of the response item to retrieve.</param>
    /// <param name="response">The retrieved OpenApiResponse if found; otherwise, null.</param>
    /// <param name="isInline">Indicates whether the response was found in inline components.</param>
    /// <returns>True if the response item was found; otherwise, false.</returns>
    private bool TryGetResponseItem(string responseName, out OpenApiResponse? response, out bool isInline)
    {
        // First, check inline components
        if (TryGetInline(name: responseName, kind: OpenApiComponentKind.Responses, out response))
        {
            isInline = true;
            return true;
        }
        // Next, check document components
        else if (TryGetComponent(name: responseName, kind: OpenApiComponentKind.Responses, out response))
        {
            isInline = false;
            return true;
        }
        response = null;
        isInline = false;
        return false;
    }
    /// <summary>
    /// Tries to get a response item by name from document components only.
    /// </summary>
    /// <param name="responseName"> The name of the response item to retrieve.</param>
    /// <param name="response"> The retrieved OpenApiResponse if found; otherwise, null.</param>
    /// <returns>True if the response item was found; otherwise, false.</returns>
    private bool TryGetResponseItem(string responseName, out OpenApiResponse? response) =>
    TryGetResponseItem(responseName, out response, out _);

    #endregion
}
