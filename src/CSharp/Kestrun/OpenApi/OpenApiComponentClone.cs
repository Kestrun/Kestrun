using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper methods for cloning OpenAPI components.
/// </summary>
public static class OpenApiComponentClone
{
    #region Parameter
    /// <summary>
    /// Clones an OpenApiParameterReference instance.
    /// </summary>
    /// <param name="parameter">The OpenApiParameterReference to clone.</param>
    /// <returns>A new OpenApiParameterReference instance with the same properties as the input parameter.</returns>
    public static OpenApiParameterReference Clone(this OpenApiParameterReference parameter)
    {
        var clone = new OpenApiParameterReference(parameter.Reference.Id!)
        {
            Description = parameter.Description,
        };
        return clone;
    }

    /// <summary>
    /// Clones an OpenApiParameter instance.
    /// </summary>
    /// <param name="parameter">The OpenApiParameter to clone.</param>
    /// <returns>A new OpenApiParameter instance with the same properties as the input parameter.</returns>
    public static OpenApiParameter Clone(this OpenApiParameter parameter)
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
            Example = JsonNodeClone(parameter.Example),
            Content = parameter.Content?.Clone(),
            Extensions = parameter.Extensions.Clone(),
            AllowEmptyValue = parameter.AllowEmptyValue,
            Deprecated = parameter.Deprecated
        };
        return clone;
    }

    /// <summary>
    /// Clones an IOpenApiParameter instance.
    /// </summary>
    /// <param name="parameter">The IOpenApiParameter instance to clone.</param>
    /// <returns>A cloned instance of IOpenApiParameter.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the IOpenApiParameter implementation is unsupported.</exception>
    public static IOpenApiParameter Clone(this IOpenApiParameter parameter)
    {
        // Determine the actual type of IOpenApiParameter and clone accordingly
        return parameter switch
        {
            OpenApiParameter param => param.Clone(),
            OpenApiParameterReference paramRef => paramRef.Clone(),
            _ => throw new InvalidOperationException("Unsupported IOpenApiParameter implementation."),
        };
    }
    #endregion
    #region RequestBody
    /// <summary>
    /// Clones an OpenApiRequestBodyReference instance.
    /// </summary>
    /// <param name="requestBody">The OpenApiRequestBodyReference to clone.</param>
    /// <returns>A new OpenApiRequestBodyReference instance with the same properties as the input requestBody.</returns>
    public static OpenApiRequestBodyReference Clone(this OpenApiRequestBodyReference requestBody)
    {
        var clone = new OpenApiRequestBodyReference(requestBody.Reference.Id!)
        {
            Description = requestBody.Description,
        };
        return clone;
    }
    /// <summary>
    /// Clones an OpenApiRequestBody instance.
    /// </summary>
    /// <param name="requestBody">The OpenApiRequestBody to clone.</param>
    /// <returns>A new OpenApiRequestBody instance with the same properties as the input requestBody.</returns>
    public static OpenApiRequestBody Clone(this OpenApiRequestBody requestBody)
    {
        var clone = new OpenApiRequestBody
        {
            Description = requestBody.Description,
            Required = requestBody.Required,
            Content = requestBody.Content.Clone(),
            Extensions = requestBody.Extensions.Clone()
        };
        return clone;
    }

    /// <summary>
    /// Converts an OpenApiRequestBody to an OpenApiSchema.
    /// </summary>
    /// <param name="requestBody">The OpenApiRequestBody to convert.</param>
    /// <returns>An OpenApiSchema representing the request body.</returns>
    public static OpenApiSchema ConvertToSchema(this OpenApiRequestBody requestBody)
    {
        var clone = new OpenApiSchema
        {
            Description = requestBody.Description,
            Properties = requestBody.Content?.Values.FirstOrDefault()?.Schema?.Properties.Clone(),
            Extensions = requestBody.Extensions.Clone()
        };
        return clone;
    }
    /// <summary>
    /// Clones an IOpenApiRequestBody instance.
    /// </summary>
    /// <param name="parameter">The IOpenApiRequestBody instance to clone.</param>
    /// <returns>A cloned instance of IOpenApiRequestBody.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the IOpenApiRequestBody implementation is unsupported.</exception>
    public static IOpenApiRequestBody Clone(this IOpenApiRequestBody parameter)
    {
        // Determine the actual type of IOpenApiParameter and clone accordingly
        return parameter switch
        {
            OpenApiRequestBody param => param.Clone(),
            OpenApiRequestBodyReference paramRef => paramRef.Clone(),
            _ => throw new InvalidOperationException("Unsupported IOpenApiRequestBody implementation."),
        };
    }
    #endregion
    #region Extensions
    /// <summary>
    /// Clones a dictionary of OpenApiExtension instances.
    /// </summary>
    /// <param name="extensions">The dictionary to clone.</param>
    /// <returns>A new dictionary with cloned OpenApiExtension instances.</returns>
    public static IDictionary<string, IOpenApiExtension>? Clone(this IDictionary<string, IOpenApiExtension>? extensions)
    {
        if (extensions == null)
        {
            return null;
        }

        var clone = new Dictionary<string, IOpenApiExtension>();
        foreach (var kvp in extensions)
        {
            clone[kvp.Key] = kvp.Value.Clone();
        }
        return clone;
    }

    /// <summary>
    /// Clones an OpenApiExtension instance.
    /// </summary>
    /// <param name="extension">The OpenApiExtension to clone.</param>
    /// <returns>A new OpenApiExtension instance with the same properties as the input extension.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the extension is of an unsupported type.</exception>
    public static IOpenApiExtension Clone(this IOpenApiExtension extension) => throw new InvalidOperationException("Unsupported IOpenApiExtension implementation.");

    #endregion

    #region Header
    /// <summary>
    /// Clones a dictionary of IOpenApiHeader instances.
    /// </summary>
    /// <param name="headers">The dictionary of headers to clone.</param>
    /// <returns>A new dictionary with cloned IOpenApiHeader instances.</returns>
    public static IDictionary<string, IOpenApiHeader>? Clone(this IDictionary<string, IOpenApiHeader>? headers)
    {
        if (headers == null)
        {
            return null;
        }

        var clone = new Dictionary<string, IOpenApiHeader>();
        foreach (var kvp in headers)
        {
            clone[kvp.Key] = kvp.Value.Clone();
        }
        return clone;
    }

    /// <summary>
    /// Clones an IOpenApiHeader instance.
    /// </summary>
    /// <param name="header">The IOpenApiHeader to clone.</param>
    /// <returns>A new IOpenApiHeader instance with the same properties as the input header.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the header is of an unsupported type.</exception>
    public static IOpenApiHeader Clone(this IOpenApiHeader header) =>
    header switch
    {
        OpenApiHeader headerObj => headerObj.Clone(),
        OpenApiHeaderReference headerRef => headerRef.Clone(),
        _ => throw new InvalidOperationException("Unsupported IOpenApiHeader implementation.")
    };

    /// <summary>
    /// Clones an OpenApiHeader instance.
    /// </summary>
    /// <param name="header">The OpenApiHeader to clone.</param>
    /// <returns>A new OpenApiHeader instance with the same properties as the input header.</returns>
    public static OpenApiHeader Clone(this OpenApiHeader header)
    {
        var clone = new OpenApiHeader
        {
            Description = header.Description,
            Required = header.Required,
            Deprecated = header.Deprecated,
            Style = header.Style,
            Explode = header.Explode,
            AllowEmptyValue = header.AllowEmptyValue,
            Schema = header.Schema?.Clone(),
            Examples = header.Examples?.Clone(),
            Example = JsonNodeClone(header.Example),
            Content = header.Content?.Clone(),
            Extensions = header.Extensions.Clone(),
            AllowReserved = header.AllowReserved
        };
        return clone;
    }
    /// <summary>
    /// Clones an OpenApiHeaderReference instance.
    /// </summary>
    /// <param name="header">The OpenApiHeaderReference to clone.</param>
    /// <returns>A new OpenApiHeaderReference instance with the same properties as the input header.</returns>
    public static OpenApiHeaderReference Clone(this OpenApiHeaderReference header)
    {
        var clone = new OpenApiHeaderReference(header.Reference.Id!)
        {
            Description = header.Description,
        };
        return clone;
    }
    #endregion
    #region Response

    /// <summary>
    /// Clones an OpenApiResponse instance.
    /// </summary>
    /// <param name="response">The OpenApiResponse to clone.</param>
    /// <returns>A new OpenApiResponse instance with the same properties as the input response.</returns>
    public static OpenApiResponse Clone(this OpenApiResponse response)
    {
        var clone = new OpenApiResponse
        {
            Description = response.Description,
            Headers = response.Headers?.Clone(),
            Content = Clone(response.Content),
            Links = response.Links.Clone(),
            Extensions = response.Extensions.Clone()
        };
        return clone;
    }
    /// <summary>
    /// Clones an OpenApiResponseReference instance.
    /// </summary>
    /// <param name="response">The OpenApiResponseReference to clone.</param>
    /// <returns>A new OpenApiResponseReference instance with the same properties as the input response.</returns>
    public static OpenApiResponseReference Clone(this OpenApiResponseReference response)
    {
        var clone = new OpenApiResponseReference(response.Reference.Id!)
        {
            Description = response.Description,
        };
        return clone;
    }

    /// <summary>
    /// Clones an IOpenApiResponse instance.
    /// </summary>
    /// <param name="response"> The IOpenApiResponse instance to clone.</param>
    /// <returns>A cloned IOpenApiResponse instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the IOpenApiResponse implementation is unsupported.</exception>
    public static IOpenApiResponse Clone(this IOpenApiResponse response)
    {
        // Determine the actual type of IOpenApiResponse and clone accordingly
        return response switch
        {
            OpenApiResponse resp => resp.Clone(),
            OpenApiResponseReference respRef => respRef.Clone(),
            _ => throw new InvalidOperationException("Unsupported IOpenApiResponse implementation."),
        };
    }

    #endregion
    #region MediaType
    /// <summary>
    /// Clones a dictionary of OpenApiMediaType instances.
    /// </summary>
    /// <param name="content">The dictionary to clone.</param>
    /// <returns>A new dictionary with cloned OpenApiMediaType instances.</returns>
    public static IDictionary<string, IOpenApiMediaType>? Clone(this IDictionary<string, IOpenApiMediaType>? content)
    {
        if (content == null)
        {
            return null;
        }

        var clone = new Dictionary<string, IOpenApiMediaType>();
        foreach (var kvp in content)
        {
            clone[kvp.Key] = kvp.Value.Clone();
        }
        return clone;
    }

    /// <summary>
    /// Clones an IOpenApiMediaType instance.
    /// </summary>
    /// <param name="mediaType"> The IOpenApiMediaType instance to clone.</param>
    /// <returns>A cloned IOpenApiMediaType instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the IOpenApiMediaType implementation is unsupported.</exception>
    public static IOpenApiMediaType Clone(this IOpenApiMediaType mediaType)
    {
        // Determine the actual type of IOpenApiParameter and clone accordingly
        return mediaType switch
        {
            OpenApiMediaType media => media.Clone(),
            OpenApiMediaTypeReference mediaRef => mediaRef.Clone(),
            _ => throw new InvalidOperationException("Unsupported IOpenApiMediaType implementation."),
        };
    }
    /// <summary>
    /// Clones an OpenApiMediaTypeReference instance.
    /// </summary>
    /// <param name="mediaType"> The OpenApiMediaTypeReference instance to clone.</param>
    /// <returns>A cloned OpenApiMediaTypeReference instance.</returns>
    public static OpenApiMediaTypeReference Clone(this OpenApiMediaTypeReference mediaType)
    {
        var clone = new OpenApiMediaTypeReference(mediaType.Reference.Id!);
        return clone;
    }

    /// <summary>
    /// Clones an OpenApiMediaType instance.
    /// </summary>
    /// <param name="mediaType">The OpenApiMediaType instance to clone.</param>
    /// <returns>A cloned OpenApiMediaType instance.</returns>
    public static OpenApiMediaType Clone(this OpenApiMediaType mediaType)
    {
        var clone = new OpenApiMediaType(mediaType);
        return clone;
    }

    #endregion
    #region Example
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
            clone[kvp.Key] = kvp.Value switch
            {
                OpenApiExample exampleObj => exampleObj.Clone(),
                OpenApiExampleReference exampleRef => exampleRef.Clone(),
                _ => throw new InvalidOperationException("Unsupported IOpenApiExample implementation."),
            };
        }
        return clone;
    }

    /// <summary>
    /// Clones an OpenApiExampleReference instance.
    /// </summary>
    /// <param name="example">The OpenApiExampleReference to clone.</param>
    /// <returns>A new OpenApiExampleReference instance with the same properties as the input instance.</returns>
    public static OpenApiExampleReference Clone(this OpenApiExampleReference example)
    {
        var clone = new OpenApiExampleReference(example.Reference.Id!)
        {
            Description = example.Description,
            Summary = example.Summary
        };
        return clone;
    }

    /// <summary>
    /// Clones an OpenApiExample instance.
    /// </summary>
    /// <param name="example">The OpenApiExample to clone.</param>
    /// <returns>A new OpenApiExample instance with the same properties as the input example.</returns>
    public static OpenApiExample Clone(this OpenApiExample example)
    {
        var clone = new OpenApiExample
        {
            Summary = example.Summary,
            Description = example.Description,
            Value = example.Value != null ? JsonNodeClone(example.Value) : null,
            ExternalValue = example.ExternalValue,
            Extensions = example.Extensions.Clone()
        };
        return clone;
    }
    #endregion
    #region Schema
    /// <summary>
    /// Clones an IOpenApiSchema instance.
    /// </summary>
    /// <param name="schema">The IOpenApiSchema to clone.</param>
    /// <returns>A new IOpenApiSchema instance with the same properties as the input schema.</returns>
    public static IOpenApiSchema Clone(this IOpenApiSchema schema) =>
    schema switch
    {
        OpenApiSchemaReference schemaRef => schemaRef.Clone(),
        OpenApiSchema schemaObj => schemaObj.Clone(),
        _ => throw new InvalidOperationException("Unsupported IOpenApiSchema implementation.")
    };

    /// <summary>
    /// Clones an OpenApiSchema instance.
    /// </summary>
    /// <param name="schema">The OpenApiSchema to clone.</param>
    /// <returns>A new OpenApiSchema instance with the same properties as the input schema.</returns>
    public static OpenApiSchema Clone(this OpenApiSchema schema)
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
            Definitions = schema.Definitions.Clone(),
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
            Default = JsonNodeClone(schema.Default),
            ReadOnly = schema.ReadOnly,
            WriteOnly = schema.WriteOnly,
            AllOf = schema.AllOf?.Clone(),
            OneOf = schema.OneOf?.Clone(),
            AnyOf = schema.AnyOf?.Clone(),
            Not = schema.Not?.Clone(),
            Required = schema.Required != null ? new HashSet<string>(schema.Required) : null,
            Items = schema.Items?.Clone(),
            MaxItems = schema.MaxItems,
            MinItems = schema.MinItems,
            UniqueItems = schema.UniqueItems,
            Properties = schema.Properties.Clone(),
            PatternProperties = schema.PatternProperties?.Clone(),
            MaxProperties = schema.MaxProperties,
            MinProperties = schema.MinProperties,
            AdditionalPropertiesAllowed = schema.AdditionalPropertiesAllowed,
            AdditionalProperties = schema.AdditionalProperties?.Clone(),
            Discriminator = schema.Discriminator != null ? new(schema.Discriminator) : null,
            Example = schema.Example != null ? JsonNodeClone(schema.Example) : null,
            Examples = schema.Examples != null ? [.. schema.Examples] : null,
            Enum = schema.Enum != null ? [.. schema.Enum] : null,
            ExternalDocs = schema.ExternalDocs != null ? new(schema.ExternalDocs) : null,
            Deprecated = schema.Deprecated,
            Xml = schema.Xml != null ? new(schema.Xml) : null,
            Extensions = schema.Extensions.Clone(),
            Metadata = schema is IMetadataContainer { Metadata: not null } mContainer ? new Dictionary<string, object>(mContainer.Metadata) : null,
            UnrecognizedKeywords = schema.UnrecognizedKeywords != null ? new Dictionary<string, JsonNode>(schema.UnrecognizedKeywords) : null,
            DependentRequired = schema.DependentRequired != null ? new Dictionary<string, HashSet<string>>(schema.DependentRequired) : null
        };
        return clone;
    }
    /// <summary>
    /// Clones an OpenApiSchemaReference instance.
    /// </summary>
    /// <param name="schemaRef">The OpenApiSchemaReference to clone</param>
    /// <returns>A new OpenApiSchemaReference instance with the same properties as the input instance.</returns>
    public static OpenApiSchemaReference Clone(this OpenApiSchemaReference schemaRef)
    {
        var cloneRef = new OpenApiSchemaReference(referenceId: schemaRef.Reference.Id!)
        {
            Reference = schemaRef.Reference,
            Title = schemaRef.Title,
            Description = schemaRef.Description
        };
        return cloneRef;
    }
    /// <summary>
    /// Clones a list of OpenApiSchema instances.
    /// </summary>
    /// <param name="schemas">The list to clone.</param>
    /// <returns>A new list containing cloned OpenApiSchema instances.</returns>
    public static IList<IOpenApiSchema>? Clone(this IList<IOpenApiSchema>? schemas)
    {
        if (schemas == null)
        {
            return null;
        }
        var cloneList = new List<IOpenApiSchema>();
        foreach (var schema in schemas)
        {
            cloneList.Add(schema.Clone());
        }
        return cloneList;
    }
    /// <summary>
    /// Clones a dictionary of OpenApiSchema instances.
    /// </summary>
    /// <param name="schemas">The dictionary to clone.</param>
    /// <returns>A new dictionary containing cloned OpenApiSchema instances.</returns>
    public static Dictionary<string, IOpenApiSchema>? Clone(this IDictionary<string, IOpenApiSchema>? schemas)
    {
        if (schemas == null)
        {
            return null;
        }
        var clone = new Dictionary<string, IOpenApiSchema>();
        foreach (var kvp in schemas)
        {
            clone[kvp.Key] = kvp.Value.Clone();
        }
        return clone;
    }
    #endregion

    #region Callback
    /// <summary>
    /// Clones an IOpenApiCallback instance.
    /// </summary>
    /// <param name="callback">The IOpenApiCallback to clone.</param>
    /// <returns>A new IOpenApiCallback instance with the same properties as the input callback.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the callback is of an unsupported type.</exception>
    public static IOpenApiCallback Clone(this IOpenApiCallback callback) =>
    callback switch
    {
        OpenApiCallback callbackObj => callbackObj.Clone(),
        OpenApiCallbackReference callbackRef => callbackRef.Clone(),
        _ => throw new InvalidOperationException("Unsupported IOpenApiCallback implementation.")
    };

    /// <summary>
    /// Clones an OpenApiCallback instance.
    /// </summary>
    /// <param name="callback">The OpenApiCallback to clone.</param>
    /// <returns>A new OpenApiCallback instance with the same properties as the input callback.</returns>
    public static OpenApiCallback Clone(this OpenApiCallback callback)
    {
        var clone = new OpenApiCallback
        {
            PathItems = callback?.PathItems != null ? new(callback.PathItems) : null,
            Extensions = callback?.Extensions.Clone()
        };
        return clone;
    }

    /// <summary>
    /// Clones an OpenApiCallbackReference instance.
    /// </summary>
    /// <param name="callback">The OpenApiCallbackReference to clone.</param>
    /// <returns>A new OpenApiCallbackReference instance with the same properties as the input callback.</returns>
    public static OpenApiCallbackReference Clone(this OpenApiCallbackReference callback)
    {
        var clone = new OpenApiCallbackReference(callback.Reference.Id!);
        return clone;
    }

    #endregion
    #region Link
    /// <summary>
    /// Clones a dictionary of IOpenApiLink instances.
    /// </summary>
    /// <param name="links">The dictionary of IOpenApiLink instances to clone.</param>
    /// <returns>A new dictionary containing cloned IOpenApiLink instances.</returns>
    public static IDictionary<string, IOpenApiLink>? Clone(this IDictionary<string, IOpenApiLink>? links)
    {
        if (links == null)
        {
            return null;
        }

        var clone = new Dictionary<string, IOpenApiLink>();
        foreach (var kvp in links)
        {
            clone[kvp.Key] = kvp.Value.Clone();
        }
        return clone;
    }

    /// <summary>
    /// Clones an IOpenApiLink instance.
    /// </summary>
    /// <param name="link">The IOpenApiLink to clone.</param>
    /// <returns>A new IOpenApiLink instance with the same properties as the input link.</returns>
    public static IOpenApiLink Clone(this IOpenApiLink link) =>
    link switch
    {
        OpenApiLink linkObj => linkObj.Clone(),
        OpenApiLinkReference linkRef => linkRef.Clone(),
        _ => throw new InvalidOperationException("Unsupported IOpenApiLink implementation.")
    };

    /// <summary>
    /// Clones an OpenApiLinkReference instance.
    /// </summary>
    /// <param name="link">The OpenApiLinkReference instance to clone.</param>
    /// <returns>A new OpenApiLinkReference instance with the same properties as the input instance.</returns>
    public static OpenApiLinkReference Clone(this OpenApiLinkReference link)
    {
        var clone = new OpenApiLinkReference(link.Reference.Id!)
        {
            Reference = link.Reference
        };
        return clone;
    }

    /// <summary>
    /// Clones an OpenApiLink instance.
    /// </summary>
    /// <param name="link">The OpenApiLink instance to clone.</param>
    /// <returns>A new OpenApiLink instance with the same properties as the input instance.</returns>
    public static OpenApiLink Clone(this OpenApiLink link)
    {
        var clone = new OpenApiLink
        {
            OperationRef = link.OperationRef,
            OperationId = link.OperationId,
            Parameters = link.Parameters.Clone(),
            RequestBody = link.RequestBody!.Clone(),
            Description = link.Description,
            Server = link.Server?.Clone(),
            Extensions = link.Extensions.Clone()
        };
        return clone;
    }
    #endregion

    /// <summary>
    /// Clones an OpenApiServer instance.
    /// </summary>
    /// <param name="server">The OpenApiServer instance to clone.</param>
    /// <returns>A new OpenApiServer instance with the same properties as the input instance.</returns>
    public static OpenApiServer Clone(this OpenApiServer server)
    {
        var clone = new OpenApiServer
        {
            Url = server.Url,
            Description = server.Description,
            Variables = server.Variables != null ? new Dictionary<string, OpenApiServerVariable>(server.Variables) : null,
            Extensions = server.Extensions.Clone()
        };
        return clone;
    }

    #region RuntimeExpression
    /// <summary>
    /// Clones a RuntimeExpressionAnyWrapper instance.
    /// </summary>
    /// <param name="expressionWrapper">The RuntimeExpressionAnyWrapper instance to clone.</param>
    /// <returns>A new RuntimeExpressionAnyWrapper instance with the same properties as the input instance.</returns>
    public static RuntimeExpressionAnyWrapper Clone(this RuntimeExpressionAnyWrapper expressionWrapper)
    {
        return new RuntimeExpressionAnyWrapper
        {
            Expression = expressionWrapper.Expression,
            Any = expressionWrapper.Any != null ? JsonNodeClone(expressionWrapper.Any) : null
        };
    }

    /// <summary>
    /// Clones a dictionary of RuntimeExpressionAnyWrapper instances.
    /// </summary>
    /// <param name="parameters">The dictionary of RuntimeExpressionAnyWrapper instances to clone.</param>
    /// <returns>A new dictionary that is a deep clone of the input dictionary.</returns>
    public static IDictionary<string, RuntimeExpressionAnyWrapper>? Clone(this IDictionary<string, RuntimeExpressionAnyWrapper>? parameters)
    {
        if (parameters == null)
        {
            return null;
        }

        var clone = new Dictionary<string, RuntimeExpressionAnyWrapper>();
        foreach (var kvp in parameters)
        {
            clone[kvp.Key] = kvp.Value.Clone();
        }
        return clone;
    }

    #endregion
    #region JsonNode
    /// <summary>
    /// Clones a JsonNode instance.
    /// </summary>
    /// <param name="value">The JsonNode to clone.</param>
    /// <returns>A new JsonNode instance that is a deep clone of the input value.</returns>
    public static JsonNode? JsonNodeClone(JsonNode? value) => value?.DeepClone();
    #endregion
}
