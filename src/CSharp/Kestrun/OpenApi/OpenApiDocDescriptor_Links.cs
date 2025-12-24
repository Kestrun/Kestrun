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
        else
        {
            throw new InvalidOperationException(
                $"Example with ReferenceId '{attribute.ReferenceId}' not found in components or inline components.");
        }
    }

    private void ApplyResponseLinkAttribute(OpenAPIMetadata metadata, OpenApiResponseLinkRefAttribute attribute)
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

    private bool ApplyLinkRefAttribute(OpenApiResponseLinkRefAttribute attribute, OpenApiResponse response)
    {
        response.Links ??= new Dictionary<string, IOpenApiLink>();
        // Clone or reference the example
        _ = TryAddLink(response.Links, attribute);

        return true;
    }
}
