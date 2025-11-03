using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper methods for cloning OpenAPI components.
/// </summary>
public static class OpenApiComponentClone
{
    /// <summary>
    /// Clones an OpenApiParameter instance.
    /// </summary>
    /// <param name="parameter">The OpenApiParameter to clone.</param>
    /// <returns>A new OpenApiParameter instance with the same properties as the input parameter.</returns>
    public static OpenApiParameter Clone(this IOpenApiParameter parameter)
    {
        var clone = new OpenApiParameter
        {
            Name = parameter.Name,
            In = parameter.In,
            Description = parameter.Description,
            Required = parameter.Required,
            Style = parameter.Style,
            Explode = parameter.Explode,
            AllowReserved = parameter.AllowReserved,
            Schema = parameter.Schema?.Clone(),
            Examples = parameter.Examples?.Clone(),
            Example = parameter.Example != null ? JsonNodeClone(parameter.Example) : null,
            Content = parameter.Content?.Clone(),
            Extensions = parameter.Extensions != null ? new Dictionary<string, IOpenApiExtension>(parameter.Extensions) : null,
            AllowEmptyValue = parameter.AllowEmptyValue,
            Deprecated = parameter.Deprecated
        };
        return clone;
    }
    /// <summary>
    /// Clones an OpenApiRequestBody instance.
    /// </summary>
    /// <param name="requestBody">The OpenApiRequestBody to clone.</param>
    /// <returns>A new OpenApiRequestBody instance with the same properties as the input requestBody.</returns>
    public static OpenApiRequestBody Clone(this IOpenApiRequestBody requestBody)
    {
        var clone = new OpenApiRequestBody
        {
            Description = requestBody.Description,
            Required = requestBody.Required,
            Content = requestBody.Content != null ? new Dictionary<string, OpenApiMediaType>(requestBody.Content) : null,
            Extensions = requestBody.Extensions != null ? new Dictionary<string, IOpenApiExtension>(requestBody.Extensions) : null
        };
        return clone;
    }

    /// <summary>
    /// Clones an OpenApiResponse instance.
    /// </summary>
    /// <param name="response">The OpenApiResponse to clone.</param>
    /// <returns>A new OpenApiResponse instance with the same properties as the input response.</returns>
    public static OpenApiResponse Clone(this IOpenApiResponse response)
    {
        var clone = new OpenApiResponse
        {
            Description = response.Description,
            Headers = response.Headers != null ? new Dictionary<string, IOpenApiHeader>(response.Headers) : null,
            Content = Clone(response.Content),
            Links = response.Links != null ? new Dictionary<string, IOpenApiLink>(response.Links) : null,
            Extensions = response.Extensions != null ? new Dictionary<string, IOpenApiExtension>(response.Extensions) : null
        };
        return clone;
    }
    /// <summary>
    /// Clones a dictionary of OpenApiMediaType instances.
    /// </summary>
    /// <param name="content">The dictionary to clone.</param>
    /// <returns>A new dictionary with cloned OpenApiMediaType instances.</returns>
    public static IDictionary<string, OpenApiMediaType>? Clone(this IDictionary<string, OpenApiMediaType>? content)
    {
        if (content == null)
        {
            return null;
        }

        var clone = new Dictionary<string, OpenApiMediaType>();
        foreach (var kvp in content)
        {
            clone[kvp.Key] = kvp.Value.Clone();
        }
        return clone;
    }

    /// <summary>
    /// Clones an OpenApiMediaType instance.
    /// </summary>
    /// <param name="mediaType">The OpenApiMediaType to clone.</param>
    /// <returns>A new OpenApiMediaType instance with the same properties as the input mediaType.</returns>
    public static OpenApiMediaType Clone(this OpenApiMediaType mediaType)
    {
        var clone = new OpenApiMediaType
        {
            Schema = mediaType.Schema?.Clone(),
            Example = mediaType.Example != null ? JsonNodeClone(mediaType.Example) : null,
            Examples = mediaType.Examples != null ? new Dictionary<string, IOpenApiExample>(mediaType.Examples) : null,
            Encoding = mediaType.Encoding != null ? new Dictionary<string, OpenApiEncoding>(mediaType.Encoding) : null,
            Extensions = mediaType.Extensions != null ? new Dictionary<string, IOpenApiExtension>(mediaType.Extensions) : null
        };
        return clone;
    }

    /// <summary>
    /// Clones a dictionary of OpenApiExample instances.
    /// </summary>
    /// <param name="examples">The dictionary to clone.</param>
    /// <returns>A new dictionary with cloned OpenApiExample instances.</returns>
    public static IDictionary<string, IOpenApiExample>? Clone(this IDictionary<string, IOpenApiExample>? examples)
    {
        if (examples == null)
        {
            return null;
        }

        var clone = new Dictionary<string, IOpenApiExample>();
        foreach (var kvp in examples)
        {
            clone[kvp.Key] = kvp.Value.Clone();
        }
        return clone;
    }

    internal static JsonNode? JsonNodeClone(JsonNode? value) => value?.DeepClone();


    /// <summary>
    /// Clones an OpenApiSchema instance.
    /// </summary>
    /// <param name="schema">The OpenApiSchema to clone.</param>
    /// <returns>A new OpenApiSchema instance with the same properties as the input schema.</returns>
    public static OpenApiSchema Clone(this IOpenApiSchema schema)
    {
        var clone = new OpenApiSchema()
        {
            Title = schema.Title,
            Id = schema.Id,
            Const = schema.Const,
            Schema = schema.Schema,
            Comment = schema.Comment,
            Vocabulary = schema.Vocabulary != null ? new Dictionary<string, bool>(schema.Vocabulary) : null,
            DynamicAnchor = schema.DynamicAnchor,
            DynamicRef = schema.DynamicRef,
            Definitions = schema.Definitions != null ? new Dictionary<string, IOpenApiSchema>(schema.Definitions) : null,
            UnevaluatedProperties = schema.UnevaluatedProperties,
            ExclusiveMaximum = schema.ExclusiveMaximum,
            ExclusiveMinimum = schema.ExclusiveMinimum,

            Type = schema.Type,
            Format = schema.Format,
            Description = schema.Description,
            Maximum = schema.Maximum,
            Minimum = schema.Minimum,
            MaxLength = schema.MaxLength,
            MinLength = schema.MinLength,
            Pattern = schema.Pattern,
            MultipleOf = schema.MultipleOf,
            Default = schema.Default != null ? JsonNodeClone(schema.Default) : null,
            ReadOnly = schema.ReadOnly,
            WriteOnly = schema.WriteOnly,
            AllOf = schema.AllOf != null ? [.. schema.AllOf] : null,
            OneOf = schema.OneOf != null ? [.. schema.OneOf] : null,
            AnyOf = schema.AnyOf != null ? [.. schema.AnyOf] : null,
            Not = schema.Not?.CreateShallowCopy(),
            Required = schema.Required != null ? new HashSet<string>(schema.Required) : null,
            Items = schema.Items?.CreateShallowCopy(),
            MaxItems = schema.MaxItems,
            MinItems = schema.MinItems,
            UniqueItems = schema.UniqueItems,
            Properties = schema.Properties != null ? new Dictionary<string, IOpenApiSchema>(schema.Properties) : null,
            PatternProperties = schema.PatternProperties != null ? new Dictionary<string, IOpenApiSchema>(schema.PatternProperties) : null,
            MaxProperties = schema.MaxProperties,
            MinProperties = schema.MinProperties,
            AdditionalPropertiesAllowed = schema.AdditionalPropertiesAllowed,
            AdditionalProperties = schema.AdditionalProperties?.CreateShallowCopy(),
            Discriminator = schema.Discriminator != null ? new(schema.Discriminator) : null,
            Example = schema.Example != null ? JsonNodeClone(schema.Example) : null,
            Examples = schema.Examples != null ? [.. schema.Examples] : null,
            Enum = schema.Enum != null ? [.. schema.Enum] : null,
            ExternalDocs = schema.ExternalDocs != null ? new(schema.ExternalDocs) : null,
            Deprecated = schema.Deprecated,
            Xml = schema.Xml != null ? new(schema.Xml) : null,
            Extensions = schema.Extensions != null ? new Dictionary<string, IOpenApiExtension>(schema.Extensions) : null,
            Metadata = schema is IMetadataContainer { Metadata: not null } mContainer ? new Dictionary<string, object>(mContainer.Metadata) : null,
            UnrecognizedKeywords = schema.UnrecognizedKeywords != null ? new Dictionary<string, JsonNode>(schema.UnrecognizedKeywords) : null,
            DependentRequired = schema.DependentRequired != null ? new Dictionary<string, HashSet<string>>(schema.DependentRequired) : null
        };
        return clone;
    }

    /// <summary>
    /// Clones an OpenApiExample instance.
    /// </summary>
    /// <param name="example">The OpenApiExample to clone.</param>
    /// <returns>A new OpenApiExample instance with the same properties as the input example.</returns>
    public static IOpenApiExample Clone(this IOpenApiExample example)
    {
        var clone = new OpenApiExample
        {
            Summary = example.Summary,
            Description = example.Description,
            Value = example.Value != null ? JsonNodeClone(example.Value) : null,
            ExternalValue = example.ExternalValue,
            Extensions = example.Extensions != null ? new Dictionary<string, IOpenApiExtension>(example.Extensions) : null
        };
        return clone;
    }
}
