using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Extension methods for OaSchemaType enum.
/// </summary>
public static class OaSchemaTypeExtensions
{
    /// <summary>
    /// Converts OaSchemaType to JsonSchemaType.
    /// </summary>
    /// <param name="schemaType">The OaSchemaType value.</param>
    /// <returns>The corresponding JsonSchemaType value, or null if OaSchemaType.None.</returns>
    public static JsonSchemaType? ToJsonSchemaType(this OaSchemaType schemaType)
    {
        return schemaType switch
        {
            OaSchemaType.String => JsonSchemaType.String,
            OaSchemaType.Number => JsonSchemaType.Number,
            OaSchemaType.Integer => JsonSchemaType.Integer,
            OaSchemaType.Boolean => JsonSchemaType.Boolean,
            OaSchemaType.Array => JsonSchemaType.Array,
            OaSchemaType.Object => JsonSchemaType.Object,
            OaSchemaType.Null => JsonSchemaType.Null,
            _ => null,
        };
    }
}
