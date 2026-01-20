using System.Collections;
using Kestrun.Hosting.Options;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Adds a component link to the OpenAPI document.
    /// </summary>
    /// <param name="name">The name of the link component.</param>
    /// <param name="link">The link component to add.</param>
    /// <param name="ifExists">The conflict resolution strategy if a link with the same name already exists.</param>
    public void AddComponentLink(
        string name,
        OpenApiLink link,
        OpenApiComponentConflictResolution ifExists = OpenApiComponentConflictResolution.Overwrite)
    {
        Document.Components ??= new OpenApiComponents();
        // Ensure Examples dictionary exists
        Document.Components.Links ??= new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
        AddComponent(Document.Components.Links, name,
                        link, ifExists,
                        OpenApiComponentKind.Links);
    }

    private bool TryAddLink(IDictionary<string, IOpenApiLink> links, OpenApiResponseLinkRefAttribute attribute)
    {
        if (TryGetInline(name: attribute.ReferenceId, kind: OpenApiComponentKind.Links, out OpenApiLink? link))
        {
            // If InlineComponents, clone the example
            return links.TryAdd(attribute.Key, link!.Clone());
        }
        else if (TryGetComponent(name: attribute.ReferenceId, kind: OpenApiComponentKind.Links, out link))
        {
            // if in main components, reference it or clone based on Inline flag
            IOpenApiLink oaLink = attribute.Inline ? link!.Clone() : new OpenApiLinkReference(attribute.ReferenceId);
            return links.TryAdd(attribute.Key, oaLink);
        }
        else if (attribute.Inline)
        {
            throw new InvalidOperationException($"Inline link component with ID '{attribute.ReferenceId}' not found.");
        }
        return false;
    }

    /// <summary>
    /// Applies an OpenApiResponseLinkRefAttribute to the specified OpenAPI path metadata.
    /// </summary>
    /// <param name="metadata">The OpenAPI path metadata to which the link attribute will be applied.</param>
    /// <param name="attribute">The OpenApiResponseLinkRefAttribute to apply.</param>
    /// <exception cref="InvalidOperationException">Thrown when the attribute is missing required properties.</exception>
    private void ApplyResponseLinkAttribute(OpenAPIPathMetadata metadata, OpenApiResponseLinkRefAttribute attribute)
    {
        if (attribute.StatusCode is null)
        {
            throw new InvalidOperationException("OpenApiLinkAttribute must have a StatusCode specified to associate the link with a response.");
        }
        if (attribute.Key is null)
        {
            throw new InvalidOperationException("OpenApiLinkRefAttribute must have a Key specified to define the link name under response.links.");
        }
        metadata.Responses ??= [];
        var response = metadata.Responses.TryGetValue(attribute.StatusCode, out var value) ? value as OpenApiResponse : new OpenApiResponse();
        if (response is not null && CreateResponseFromAttribute(attribute, response))
        {
            _ = metadata.Responses.TryAdd(attribute.StatusCode, response);
        }
    }

    /// <summary>
    /// Applies an OpenApiResponseLinkRefAttribute to the specified OpenAPI response.
    /// </summary>
    /// <param name="attribute">The OpenApiResponseLinkRefAttribute to apply.</param>
    /// <param name="response">The OpenAPI response to which the link attribute will be applied.</param>
    /// <returns>True if the link was successfully applied; otherwise, false.</returns>
    private bool ApplyLinkRefAttribute(OpenApiResponseLinkRefAttribute attribute, OpenApiResponse response)
    {
        response.Links ??= new Dictionary<string, IOpenApiLink>();
        // Clone or reference the example
        _ = TryAddLink(response.Links, attribute);

        return true;
    }

    /// <summary>
    /// Creates a new OpenApiLink instance based on the provided parameters.
    /// </summary>
    /// <param name="operationRef">Operation reference string.</param>
    /// <param name="operationId">Operation identifier string.</param>
    /// <param name="description">Description of the link.</param>
    /// <param name="server">Server object associated with the link.</param>
    /// <param name="parameters">Parameters dictionary for the link.</param>
    /// <param name="requestBody">Request body object or expression.</param>
    /// <param name="extensions">Extensions dictionary for the link.</param>
    /// <returns>Newly created OpenApiLink instance.</returns>
    /// <exception cref="ArgumentException">Thrown when both operationRef and operationId are provided.</exception>
    public OpenApiLink NewOpenApiLink(
           string? operationRef,
           string? operationId,
           string? description,
           OpenApiServer? server,
           IDictionary? parameters,
           object? requestBody,
           IDictionary? extensions)
    {
        ValidateLinkOperation(operationRef, operationId);

        var link = new OpenApiLink
        {
            Extensions = BuildExtensions(extensions)
        };

        ApplyLinkDescription(link, description);
        ApplyLinkServer(link, server);
        ApplyLinkOperation(link, operationRef, operationId);
        ApplyLinkRequestBody(link, requestBody);
        ApplyLinkParameters(link, parameters);

        return link;
    }

    /// <summary>
    /// Validates that exactly one of <paramref name="operationRef"/> or <paramref name="operationId"/> is provided.
    /// </summary>
    /// <param name="operationRef">The operation reference string.</param>
    /// <param name="operationId">The operation identifier string.</param>
    /// <exception cref="ArgumentException">Thrown when both are provided, or when neither is provided.</exception>
    private static void ValidateLinkOperation(string? operationRef, string? operationId)
    {
        // Match the PS safety rule.
        if (!string.IsNullOrWhiteSpace(operationRef) && !string.IsNullOrWhiteSpace(operationId))
        {
            throw new ArgumentException("OperationId and OperationRef are mutually exclusive in an OpenAPI Link.");
        }

        if (string.IsNullOrWhiteSpace(operationRef) && string.IsNullOrWhiteSpace(operationId))
        {
            // Should be prevented by parameter sets, but keep it robust.
            throw new ArgumentException("Either OperationRef or OperationId must be provided.");
        }
    }

    /// <summary>
    /// Applies the description to the link when provided.
    /// </summary>
    /// <param name="link">The link to update.</param>
    /// <param name="description">The description value.</param>
    private static void ApplyLinkDescription(OpenApiLink link, string? description)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            link.Description = description;
        }
    }

    /// <summary>
    /// Applies the server to the link when provided.
    /// </summary>
    /// <param name="link">The link to update.</param>
    /// <param name="server">The server value.</param>
    private static void ApplyLinkServer(OpenApiLink link, OpenApiServer? server)
    {
        if (server is not null)
        {
            link.Server = server;
        }
    }

    /// <summary>
    /// Applies <see cref="OpenApiLink.OperationRef"/> or <see cref="OpenApiLink.OperationId"/>.
    /// </summary>
    /// <param name="link">The link to update.</param>
    /// <param name="operationRef">The operation reference string.</param>
    /// <param name="operationId">The operation identifier string.</param>
    private static void ApplyLinkOperation(OpenApiLink link, string? operationRef, string? operationId)
    {
        if (!string.IsNullOrWhiteSpace(operationRef))
        {
            link.OperationRef = operationRef;
            return;
        }

        if (!string.IsNullOrWhiteSpace(operationId))
        {
            link.OperationId = operationId;
        }
    }

    /// <summary>
    /// Applies the request body to the link, interpreting string values as runtime expressions.
    /// </summary>
    /// <param name="link">The link to update.</param>
    /// <param name="requestBody">The request body value.</param>
    private static void ApplyLinkRequestBody(OpenApiLink link, object? requestBody)
    {
        if (requestBody is null)
        {
            return;
        }

        var wrapper = new RuntimeExpressionAnyWrapper();

        if (requestBody is string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return;
            }

            wrapper.Expression = RuntimeExpression.Build(s);
            link.RequestBody = wrapper;
            return;
        }

        wrapper.Any = OpenApiJsonNodeFactory.ToNode(requestBody);
        link.RequestBody = wrapper;
    }

    /// <summary>
    /// Applies link parameters, interpreting string values as runtime expressions.
    /// </summary>
    /// <param name="link">The link to update.</param>
    /// <param name="parameters">The parameters dictionary.</param>
    private static void ApplyLinkParameters(OpenApiLink link, IDictionary? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return;
        }

        link.Parameters ??= new Dictionary<string, RuntimeExpressionAnyWrapper>(StringComparer.Ordinal);

        foreach (DictionaryEntry entry in parameters)
        {
            if (entry.Key is null)
            {
                continue;
            }

            var key = entry.Key.ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            link.Parameters[key] = ToRuntimeExpressionAnyWrapper(entry.Value);
        }
    }

    /// <summary>
    /// Converts a value into a <see cref="RuntimeExpressionAnyWrapper"/>.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    /// <returns>A wrapper containing either a runtime expression or an OpenAPI JSON node.</returns>
    private static RuntimeExpressionAnyWrapper ToRuntimeExpressionAnyWrapper(object? value)
    {
        var wrapper = new RuntimeExpressionAnyWrapper();

        if (value is string expr)
        {
            wrapper.Expression = RuntimeExpression.Build(expr);
        }
        else
        {
            wrapper.Any = OpenApiJsonNodeFactory.ToNode(value);
        }

        return wrapper;
    }
}
