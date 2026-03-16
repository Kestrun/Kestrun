using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Kestrun.OpenApi;

/// <summary>
/// Helper methods for creating shallow copies of OpenAPI components.
/// </summary>
internal static class OpenApiComponentShallowCopy
{
    /// <summary>
    /// Converts an OpenApiRequestBody to an OpenApiSchema.
    /// </summary>
    /// <param name="requestBody">The OpenApiRequestBody to convert.</param>
    /// <returns>An OpenApiSchema representing the request body.</returns>
    internal static OpenApiSchema ConvertToSchema(this OpenApiRequestBody requestBody)
    {
        var clone = new OpenApiSchema
        {
            Description = requestBody.Description,
            Properties = requestBody.Content?.Values.FirstOrDefault()?.Schema?.Properties.CreateShallowCopy(),
            Extensions = requestBody.Extensions.CreateShallowCopy()
        };
        return clone;
    }

    /// <summary>
    /// Creates a shallow copy of a dictionary of OpenApiExtension instances.
    /// </summary>
    /// <param name="extensions">The dictionary to copy.</param>
    /// <returns>A new dictionary with shallow copies of the OpenApiExtension instances.</returns>
    internal static IDictionary<string, IOpenApiExtension>? CreateShallowCopy(this IDictionary<string, IOpenApiExtension>? extensions)
    {
        if (extensions == null)
        {
            return null;
        }

        var clone = new Dictionary<string, IOpenApiExtension>();
        foreach (var kvp in extensions)
        {
            clone[kvp.Key] = kvp.Value.CreateShallowCopy();
        }
        return clone;
    }

    /// <summary>
    /// Creates a shallow copy of an OpenApiExtension instance.
    /// </summary>
    /// <param name="extension">The OpenApiExtension to copy.</param>
    /// <returns>A new OpenApiExtension instance with the same properties as the input extension.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the extension is of an unsupported type.</exception>
    internal static IOpenApiExtension CreateShallowCopy(this IOpenApiExtension extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        if (extension is JsonNodeExtension jsonNodeExtension)
        {
            var nodeClone = DeepClone(jsonNodeExtension.Node)
                ?? throw new InvalidOperationException("Unsupported IOpenApiExtension implementation.");
            return new JsonNodeExtension(nodeClone);
        }

        throw new InvalidOperationException("Unsupported IOpenApiExtension implementation.");
    }

    /// <summary>
    /// Creates a shallow copy of a dictionary of IOpenApiHeader instances.
    /// </summary>
    /// <param name="headers">The dictionary of headers to copy.</param>
    /// <returns>A new dictionary with shallow copies of the IOpenApiHeader instances.</returns>
    internal static IDictionary<string, IOpenApiHeader>? CreateShallowCopy(this IDictionary<string, IOpenApiHeader>? headers)
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

    /// <summary>
    /// Creates a shallow copy of a dictionary of OpenApiMediaType instances.
    /// </summary>
    /// <param name="content">The dictionary to copy.</param>
    /// <returns>A new dictionary with shallow copies of the OpenApiMediaType instances.</returns>
    internal static IDictionary<string, IOpenApiMediaType>? CreateShallowCopy(this IDictionary<string, IOpenApiMediaType>? content)
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

    /// <summary>
    /// Creates a shallow copy of a dictionary of OpenApiExample instances.
    /// </summary>
    /// <param name="examples">The dictionary to copy.</param>
    /// <returns>A new dictionary with shallow copies of the OpenApiExample instances.</returns>
    internal static IDictionary<string, IOpenApiExample>? CreateShallowCopy(this IDictionary<string, IOpenApiExample>? examples)
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

    /// <summary>
    /// Creates a shallow copy of a list of OpenApiSchema instances.
    /// </summary>
    /// <param name="schemas">The list to copy.</param>
    /// <returns>A new list containing shallow copies of the OpenApiSchema instances.</returns>
    internal static IList<IOpenApiSchema>? CreateShallowCopy(this IList<IOpenApiSchema>? schemas)
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
    /// Creates a shallow copy of a dictionary of OpenApiSchema instances.
    /// </summary>
    /// <param name="schemas">The dictionary to copy.</param>
    /// <returns>A new dictionary containing shallow copies of the OpenApiSchema instances.</returns>
    internal static Dictionary<string, IOpenApiSchema>? CreateShallowCopy(this IDictionary<string, IOpenApiSchema>? schemas)
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

    /// <summary>
    /// Creates a shallow copy of a dictionary of IOpenApiLink instances.
    /// </summary>
    /// <param name="links">The dictionary of IOpenApiLink instances to copy.</param>
    /// <returns>A new dictionary containing shallow copies of the IOpenApiLink instances.</returns>
    internal static IDictionary<string, IOpenApiLink>? CreateShallowCopy(this IDictionary<string, IOpenApiLink>? links)
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

    /// <summary>
    /// Creates a shallow copy of an OpenApiServer instance.
    /// </summary>
    /// <param name="server">The OpenApiServer instance to copy.</param>
    /// <returns>A new OpenApiServer instance with the same properties as the input instance.</returns>
    internal static OpenApiServer CreateShallowCopy(this OpenApiServer server)
    {
        var clone = new OpenApiServer
        {
            Url = server.Url,
            Description = server.Description,
            Extensions = server.Extensions.CreateShallowCopy()
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

    /// <summary>
    /// Creates a shallow copy of a RuntimeExpressionAnyWrapper instance.
    /// </summary>
    /// <param name="expressionWrapper">The RuntimeExpressionAnyWrapper instance to copy.</param>
    /// <returns>A new RuntimeExpressionAnyWrapper instance with the same properties as the input instance.</returns>
    internal static RuntimeExpressionAnyWrapper CreateShallowCopy(this RuntimeExpressionAnyWrapper expressionWrapper)
    {
        return new RuntimeExpressionAnyWrapper
        {
            Expression = expressionWrapper.Expression,
            Any = expressionWrapper.Any != null ? DeepClone(expressionWrapper.Any) : null
        };
    }

    /// <summary>
    /// Creates a shallow copy of a dictionary of RuntimeExpressionAnyWrapper instances.
    /// </summary>
    /// <param name="parameters">The dictionary of RuntimeExpressionAnyWrapper instances to copy.</param>
    /// <returns>A new dictionary containing shallow copies of the RuntimeExpressionAnyWrapper instances.</returns>
    internal static IDictionary<string, RuntimeExpressionAnyWrapper>? CreateShallowCopy(this IDictionary<string, RuntimeExpressionAnyWrapper>? parameters)
    {
        if (parameters == null)
        {
            return null;
        }

        var clone = new Dictionary<string, RuntimeExpressionAnyWrapper>();
        foreach (var kvp in parameters)
        {
            clone[kvp.Key] = kvp.Value.CreateShallowCopy();
        }
        return clone;
    }

    /// <summary>
    /// Clones a JsonNode instance.
    /// </summary>
    /// <param name="value">The JsonNode to clone.</param>
    /// <returns>A new JsonNode instance that is a deep clone of the input value.</returns>
    internal static JsonNode? DeepClone(JsonNode? value) => value?.DeepClone();
}
