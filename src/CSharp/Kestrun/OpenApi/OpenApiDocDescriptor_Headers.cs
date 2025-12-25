using System.Collections;
using System.Reflection;
using Kestrun.Hosting.Options;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Creates a new OpenApiHeader with the specified properties.
    /// </summary>
    /// <param name="description">The description of the header.</param>
    /// <param name="required">Indicates whether the header is required.</param>
    /// <param name="deprecated">Indicates whether the header is deprecated.</param>
    /// <param name="allowEmptyValue">Indicates whether empty values are allowed.</param>
    /// <param name="style">The style of the header.</param>
    /// <param name="explode">Indicates whether the header should be exploded.</param>
    /// <param name="allowReserved">Indicates whether the header allows reserved characters.</param>
    /// <param name="example">An example of the header's value.</param>
    /// <param name="examples">A collection of examples for the header.</param>
    /// <param name="schema">The schema of the header.</param>
    /// <param name="content">The content of the header.</param>
    /// <returns>A new instance of OpenApiHeader with the specified properties.</returns>
    /// <exception cref="InvalidOperationException">Thrown when header examples keys or values are invalid.</exception>
    public OpenApiHeader NewOpenApiHeader(
        string? description = null,
        bool required = false,
        bool deprecated = false,
        bool allowEmptyValue = false,
        ParameterStyle? style = null,
        bool explode = false,
        bool allowReserved = false,
        object? example = null,
        Hashtable? examples = null,
        Type? schema = null,
        Hashtable? content = null)
    {
        if (schema is null && content is null)
        {
            schema = typeof(string);
        }
        if (schema is not null && content is not null)
        {
            throw new InvalidOperationException("Cannot specify both schema and content for an OpenApiHeader.");
        }
        var header = new OpenApiHeader
        {
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            Required = required,
            Deprecated = deprecated,
            AllowEmptyValue = allowEmptyValue,
            Style = style,
            Explode = explode,
            AllowReserved = allowReserved,
            Example = OpenApiJsonNodeFactory.FromObject(example)
        };
        if (schema is not null)
        {
            header.Schema = InferPrimitiveSchema(schema);
        }
        // Multi examples from PowerShell hashtable
        if (examples is not null && examples.Count > 0)
        {
            header.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);

            foreach (var lkey in examples.Keys)
            {
                if (lkey is not string key)
                {
                    throw new InvalidOperationException("Header examples keys must be strings.");
                }
                var value = examples[key];
                if (value is IOpenApiExample ex)
                {
                    header.Examples[key] = ex;
                }
                else if (value is string exempleref)
                {
                    if (TryGetInline(name: exempleref, kind: OpenApiComponentKind.Examples, out OpenApiExample? lexemple))
                    {
                        // If InlineComponents, clone the example
                        header.Examples[key] = lexemple!.Clone();
                    }
                    else if (TryGetComponent(name: exempleref, kind: OpenApiComponentKind.Examples, out lexemple))
                    {
                        // if in main components, reference it or clone based on Inline flag
                        header.Examples[key] = new OpenApiExampleReference(exempleref);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Example with ReferenceId '{exempleref}' not found in components or inline components.");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Header examples values must be OpenApiExample or OpenApiExampleReference instances or example reference name strings.");
                }
            }
        }

        // Header content (media type map) from PowerShell hashtable
        if (content is not null && content.Count > 0)
        {
            header.Content ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);
            foreach (var lkey in content.Keys)
            {
                if (lkey is not string key)
                {
                    throw new InvalidOperationException("Header content keys must be media type strings.");
                }
                var value = content[key];
                if (value is IOpenApiMediaType mt)
                {
                    header.Content[key] = mt;
                }
                else if (value is string mediaRef)
                {
                    if (TryGetInline(name: mediaRef, kind: OpenApiComponentKind.MediaTypes, out OpenApiMediaType? lmediatype))
                    {
                        // If InlineComponents, clone the example
                        header.Content[key] = lmediatype!.Clone();
                    }
                    else if (TryGetComponent(name: mediaRef, kind: OpenApiComponentKind.MediaTypes, out lmediatype))
                    {
                        // if in main components, reference it or clone based on Inline flag
                        header.Content[key] = lmediatype!.Clone();
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"MediaType with ReferenceId '{mediaRef}' not found in components or inline components.");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Header content values must be OpenApiMediaType instances or media type reference name strings.");
                }
            }
        }
        return header;
    }

    /// <summary>
    /// Adds an OpenApiHeader component to the OpenAPI document.
    /// </summary>
    /// <param name="name"> The name of the header component. </param>
    /// <param name="header"> The OpenApiHeader object to add. </param>
    /// <param name="ifExists"> Conflict resolution strategy if the component already exists. </param>
    public void AddComponentHeader(
    string name,
    OpenApiHeader header,
    OpenApiComponentConflictResolution ifExists = OpenApiComponentConflictResolution.Overwrite)
    {
        Document.Components ??= new OpenApiComponents();
        // Ensure Examples dictionary exists
        Document.Components.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
        AddComponent(Document.Components.Headers, name,
                        header, ifExists,
                        OpenApiComponentKind.Headers);
    }

    private void ApplyResponseHeaderAttribute(OpenAPIMetadata metadata, IOpenApiResponseHeaderAttribute attribute)
    {
        if (attribute.StatusCode is null)
        {
            throw new InvalidOperationException("OpenApiResponseHeaderRefAttribute must have a StatusCode specified to associate the header reference with a response.");
        }
        if (attribute.Key is null)
        {
            throw new InvalidOperationException("OpenApiResponseHeaderRefAttribute must have a Key specified to define the header name under response.headers.");
        }
        metadata.Responses ??= [];
        var response = metadata.Responses.TryGetValue(attribute.StatusCode, out var value) ? value as OpenApiResponse : new OpenApiResponse();
        if (response is not null && CreateResponseFromAttribute(attribute, response))
        {
            _ = metadata.Responses.TryAdd(attribute.StatusCode, response);
        }
    }

    private bool TryAddHeader(IDictionary<string, IOpenApiHeader> headers, OpenApiResponseHeaderRefAttribute attribute)
    {
        if (TryGetInline(name: attribute.ReferenceId, kind: OpenApiComponentKind.Headers, out OpenApiHeader? header))
        {
            // If InlineComponents, clone the example
            return headers.TryAdd(attribute.Key, header!.Clone());
        }
        else if (TryGetComponent(name: attribute.ReferenceId, kind: OpenApiComponentKind.Headers, out header))
        {
            // if in main components, reference it or clone based on Inline flag
            IOpenApiHeader oaHeader = attribute.Inline ? header!.Clone() : new OpenApiHeaderReference(attribute.ReferenceId);
            return headers.TryAdd(attribute.Key, oaHeader);
        }
        else
        {
            throw new InvalidOperationException(
                $"Header with ReferenceId '{attribute.ReferenceId}' not found in components or inline components.");
        }
    }
}
