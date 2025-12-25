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
        schema = ResolveHeaderSchema(schema, content);
        ThrowIfBothSchemaAndContentProvided(schema, content);

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

        ApplyHeaderSchema(header, schema);
        ApplyHeaderExamples(header, examples);
        ApplyHeaderContent(header, content);

        return header;
    }

    /// <summary>
    /// Resolves the schema for an OpenApiHeader.
    /// </summary>
    /// <param name="schema">The schema of the header.</param>
    /// <param name="content">The content of the header.</param>
    /// <returns>The resolved schema type.</returns>
    private static Type? ResolveHeaderSchema(Type? schema, Hashtable? content)
    {
        return schema is null && content is null
            ? typeof(string)
            : schema;
    }

    /// <summary>
    /// Throws an exception if both schema and content are provided for an OpenApiHeader.
    /// </summary>
    /// <param name="schema">The schema of the header.</param>
    /// <param name="content">The content of the header.</param>
    /// <exception cref="InvalidOperationException">Thrown when both schema and content are provided.</exception>
    private static void ThrowIfBothSchemaAndContentProvided(Type? schema, Hashtable? content)
    {
        if (schema is not null && content is not null)
        {
            throw new InvalidOperationException("Cannot specify both schema and content for an OpenApiHeader.");
        }
    }

    /// <summary>
    /// Applies schema to the given OpenApiHeader.
    /// </summary>
    /// <param name="header">The OpenApiHeader to which the schema will be applied.</param>
    /// <param name="schema">The schema to apply to the header.</param>
    private void ApplyHeaderSchema(OpenApiHeader header, Type? schema)
    {
        if (schema is not null)
        {
            header.Schema = InferPrimitiveSchema(schema);
        }
    }

    /// <summary>
    /// Applies examples to the given OpenApiHeader from a PowerShell hashtable.
    /// </summary>
    /// <param name="header">The OpenApiHeader to which examples will be applied.</param>
    /// <param name="examples">A hashtable representing the examples to apply.</param>
    /// <exception cref="InvalidOperationException">Thrown when example keys are not strings or values are invalid.</exception>
    private void ApplyHeaderExamples(OpenApiHeader header, Hashtable? examples)
    {
        // Multi examples from PowerShell hashtable
        if (examples is null || examples.Count == 0)
        {
            return;
        }

        header.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);

        foreach (var rawKey in examples.Keys)
        {
            if (rawKey is not string key)
            {
                throw new InvalidOperationException("Header examples keys must be strings.");
            }

            header.Examples[key] = ResolveHeaderExampleValue(examples[key]);
        }
    }

    /// <summary>
    /// Resolves an example value for a header example entry.
    /// </summary>
    /// <param name="value">The example value, which can be an IOpenApiExample or a reference string.</param>
    /// <returns>The resolved IOpenApiExample.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the example value is invalid or not found.</exception>
    private IOpenApiExample ResolveHeaderExampleValue(object? value)
    {
        if (value is IOpenApiExample example)
        {
            return example;
        }

        if (value is string exampleRef)
        {
            if (TryGetInline(name: exampleRef, kind: OpenApiComponentKind.Examples, out OpenApiExample? inlineExample))
            {
                // If InlineComponents, clone the example
                return inlineExample!.Clone();
            }

            if (TryGetComponent(name: exampleRef, kind: OpenApiComponentKind.Examples, out OpenApiExample? componentExample))
            {
                // if in main components, reference it
                _ = componentExample;
                return new OpenApiExampleReference(exampleRef);
            }

            throw new InvalidOperationException(
                $"Example with ReferenceId '{exampleRef}' not found in components or inline components.");
        }

        throw new InvalidOperationException(
            "Header examples values must be OpenApiExample or OpenApiExampleReference instances or example reference name strings.");
    }

    /// <summary>
    /// Applies content to the given OpenApiHeader from a PowerShell hashtable.
    /// </summary>
    /// <param name="header">The OpenApiHeader to which content will be applied.</param>
    /// <param name="content">A hashtable representing the content to apply.</param>
    /// <exception cref="InvalidOperationException">Thrown when content keys are not valid media type strings.</exception>

    private void ApplyHeaderContent(OpenApiHeader header, Hashtable? content)
    {
        // Header content (media type map) from PowerShell hashtable
        if (content is null || content.Count == 0)
        {
            return;
        }

        header.Content ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);

        foreach (var rawKey in content.Keys)
        {
            if (rawKey is not string key)
            {
                throw new InvalidOperationException("Header content keys must be media type strings.");
            }

            header.Content[key] = ResolveHeaderMediaTypeValue(content[key]);
        }
    }

    /// <summary>
    /// Resolves a media type value for a header content entry.
    /// </summary>
    /// <param name="value">The media type value, which can be an IOpenApiMediaType or a reference string.</param>
    /// <returns>The resolved IOpenApiMediaType.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the media type value is invalid or not found.</exception>
    private IOpenApiMediaType ResolveHeaderMediaTypeValue(object? value)
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
