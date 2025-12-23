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
}
