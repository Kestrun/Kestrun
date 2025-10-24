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
            Schema = parameter.Schema?.CreateShallowCopy(),
            Examples = parameter.Examples != null ? new Dictionary<string, IOpenApiExample>(parameter.Examples) : null,
            Example = parameter.Example != null ? JsonNodeClone(parameter.Example) : null,
            Content = parameter.Content != null ? new Dictionary<string, OpenApiMediaType>(parameter.Content) : null,
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
            Content = response.Content != null ? new Dictionary<string, OpenApiMediaType>(response.Content) : null,
            Links = response.Links != null ? new Dictionary<string, IOpenApiLink>(response.Links) : null,
            Extensions = response.Extensions != null ? new Dictionary<string, IOpenApiExtension>(response.Extensions) : null
        };
        return clone;
    }
    internal static JsonNode? JsonNodeClone(JsonNode? value) => value?.DeepClone();
}
