// File: OpenApiBuilders.cs
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Helpers to build common OpenAPI objects.
/// </summary>
public static class OpenApiBuilders
{
    /// <summary>Create a JSON response that $refs a schema by id (in components/schemas).</summary>
    public static IOpenApiResponse JsonResponseRef(string schemaId, string description = "OK")
    {
        return new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchemaReference(schemaId)
                }
            }
        };
    }

    /// <summary>Create a simple query parameter.</summary>
    public static IOpenApiParameter QueryParam(string name, string description, string? format = null)
    {
        return new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Query,     // v2 uses this enum-like type
            Description = description,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = format
            }
        };
    }
}
