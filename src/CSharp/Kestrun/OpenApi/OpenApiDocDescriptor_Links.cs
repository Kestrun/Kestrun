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
        // Match your PS safety rule
        if (!string.IsNullOrWhiteSpace(operationRef) && !string.IsNullOrWhiteSpace(operationId))
        {
            throw new ArgumentException("OperationId and OperationRef are mutually exclusive in an OpenAPI Link.");
        }

        var link = new OpenApiLink();

        if (!string.IsNullOrWhiteSpace(description))
        {
            link.Description = description;
        }

        if (server is not null)
        {
            link.Server = server;
        }

        if (!string.IsNullOrWhiteSpace(operationRef))
        {
            link.OperationRef = operationRef;
        }
        else if (!string.IsNullOrWhiteSpace(operationId))
        {
            link.OperationId = operationId;
        }
        else
        {
            // Should be prevented by parameter sets, but keep it robust.
            throw new ArgumentException("Either OperationRef or OperationId must be provided.");
        }

        // RequestBody: runtime expression string OR literal object
        if (requestBody is not null)
        {
            var rbWrapper = new RuntimeExpressionAnyWrapper();

            if (requestBody is string s && !string.IsNullOrWhiteSpace(s))
            {
                rbWrapper.Expression = RuntimeExpression.Build(s);
                link.RequestBody = rbWrapper;
            }
            else if (requestBody is not string)
            {
                rbWrapper.Any = OpenApiJsonNodeFactory.ToNode(requestBody);
                link.RequestBody = rbWrapper;
            }
        }

        // Parameters
        if (parameters is not null && parameters.Count > 0)
        {
            link.Parameters ??= new Dictionary<string, RuntimeExpressionAnyWrapper>(StringComparer.Ordinal);

            foreach (DictionaryEntry entry in parameters)
            {
                if (entry.Key is null)
                {
                    continue;
                }

                var key = entry.Key.ToString() ?? string.Empty;
                if (key.Length == 0)
                {
                    continue;
                }

                var value = entry.Value;
                var pWrapper = new RuntimeExpressionAnyWrapper();

                if (value is string expr)
                {
                    pWrapper.Expression = RuntimeExpression.Build(expr);
                }
                else
                {
                    pWrapper.Any = OpenApiJsonNodeFactory.ToNode(value);
                }

                link.Parameters[key] = pWrapper;
            }
        }
        // Extensions
        link.Extensions = BuildExtensions(extensions);
        return link;
    }
}
