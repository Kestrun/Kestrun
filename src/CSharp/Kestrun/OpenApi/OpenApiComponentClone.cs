using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper methods for cloning OpenAPI components.
/// </summary>
public static class OpenApiComponentClone
{
    #region Parameter
    #endregion
    #region RequestBody
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
    public static IOpenApiExtension Clone(this IOpenApiExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        if (extension is JsonNodeExtension jsonNodeExtension)
        {
            var nodeClone = JsonNodeClone(jsonNodeExtension.Node)
                ?? throw new InvalidOperationException("Unsupported IOpenApiExtension implementation.");
            return new JsonNodeExtension(nodeClone);
        }

        throw new InvalidOperationException("Unsupported IOpenApiExtension implementation.");
    }

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
            clone[kvp.Key] = kvp.Value.CreateShallowCopy();
        }
        return clone;
    }


    #endregion
    #region Response

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
            clone[kvp.Key] = kvp.Value.CreateShallowCopy();
        }
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
            clone[kvp.Key] = kvp.Value.CreateShallowCopy();
        }
        return clone;
    }
    #endregion
    #region Schema
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
            cloneList.Add(schema.CreateShallowCopy());
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
            clone[kvp.Key] = kvp.Value.CreateShallowCopy();
        }
        return clone;
    }
    #endregion

    #region Callback

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
            clone[kvp.Key] = kvp.Value.CreateShallowCopy();
        }
        return clone;
    }
    #endregion
    #region Server
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
            Extensions = server.Extensions.Clone()
        };
        // Clone Variables dictionary if it exists
        if (server.Variables != null)
        {
            clone.Variables = new Dictionary<string, OpenApiServerVariable>();
            foreach (var variable in server.Variables)
            {
                clone.Variables[variable.Key] = new OpenApiServerVariable(variable.Value);
            }
        }
        return clone;
    }

    #endregion
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
