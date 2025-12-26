using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Resolves a media type value for a header content entry.
    /// </summary>
    /// <param name="value">The media type value, which can be an IOpenApiMediaType or a reference string.</param>
    /// <returns>The resolved IOpenApiMediaType.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the media type value is invalid or not found.</exception>
    private IOpenApiMediaType ResolveMediaTypeValue(object? value)
    {
        if (value is IOpenApiMediaType mediaType)
        {
            return mediaType;
        }

        if (value is string mediaRef)
        {
            if (TryGetInline(name: mediaRef, kind: OpenApiComponentKind.MediaTypes, out OpenApiMediaType? inlineMediaType))
            {
                // If InlineComponents, clone the media type
                return inlineMediaType!.Clone();
            }

            if (TryGetComponent(name: mediaRef, kind: OpenApiComponentKind.MediaTypes, out OpenApiMediaType? componentMediaType))
            {
                // if in main components, clone it
                return componentMediaType!.Clone();
            }

            throw new InvalidOperationException(
                $"MediaType with ReferenceId '{mediaRef}' not found in components or inline components.");
        }

        throw new InvalidOperationException(
            "Header content values must be OpenApiMediaType instances or media type reference name strings.");
    }

    /// <summary>
    /// Resolves a header type value for a response header entry.
    /// </summary>
    /// <param name="value">The header value, which can be an IOpenApiHeader or a reference string.</param>
    /// <returns> The resolved IOpenApiHeader.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the header value is invalid or not found.</exception>
    private IOpenApiHeader ResolveHeaderTypeValue(object? value)
    {
        if (value is IOpenApiHeader header)
        {
            return header;
        }

        if (value is string headerRef)
        {
            if (TryGetInline(name: headerRef, kind: OpenApiComponentKind.Headers, out OpenApiHeader? inlineHeader))
            {
                // If InlineComponents, clone the header
                return inlineHeader!.Clone();
            }

            if (TryGetComponent(name: headerRef, kind: OpenApiComponentKind.Headers, out OpenApiHeader? componentHeader))
            {
                // if in main components, clone it
                return componentHeader!.Clone();
            }

            throw new InvalidOperationException(
                $"Header with ReferenceId '{headerRef}' not found in components or inline components.");
        }

        throw new InvalidOperationException(
            "Header content values must be OpenApiHeader instances or header reference name strings.");
    }

    /// <summary>
    /// Resolves a link type value for a response link entry.
    /// </summary>
    /// <param name="value">The link value, which can be an IOpenApiLink or a reference string.</param>
    /// <returns> The resolved IOpenApiLink.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the link value is invalid or not found.</exception>
    private IOpenApiLink ResolveLinkTypeValue(object? value)
    {
        if (value is IOpenApiLink link)
        {
            return link;
        }

        if (value is string linkRef)
        {
            if (TryGetInline(name: linkRef, kind: OpenApiComponentKind.Links, out OpenApiLink? inlineLink))
            {
                // If InlineComponents, clone the link
                return inlineLink!.Clone();
            }

            if (TryGetComponent(name: linkRef, kind: OpenApiComponentKind.Links, out OpenApiLink? componentLink))
            {
                // if in main components, clone it
                return componentLink!.Clone();
            }

            throw new InvalidOperationException(
                $"Link with ReferenceId '{linkRef}' not found in components or inline components.");
        }

        throw new InvalidOperationException(
            "Link content values must be OpenApiLink instances or link reference name strings.");
    }
}
