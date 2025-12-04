using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

public partial class OpenApiDocDescriptor
{
    private void ApplyAdditionalProperties(OpenApiSchema schema, OpenApiSchemaComponent attr)
    {
        // Are we only toggling the bool, or describing a full schema?
        var hasDetails =
              attr.AdditionalPropertiesType is not null ||
            !string.IsNullOrWhiteSpace(attr.AdditionalPropertiesFormat) ||
            !string.IsNullOrWhiteSpace(attr.AdditionalPropertiesRef) ||
            attr.AdditionalPropertiesClrType is not null ||
            attr.AdditionalPropertiesIsArray ||
            !string.IsNullOrWhiteSpace(attr.AdditionalPropertiesDescription) ||
            (attr.AdditionalPropertiesEnum is { Length: > 0 });

        if (!hasDetails)
        {
            // Backward-compatible: only the boolean matters
            schema.AdditionalPropertiesAllowed |= attr.AdditionalPropertiesAllowed;
            return;
        }

        // If user explicitly disabled additional properties, honor it
        if (!attr.AdditionalPropertiesAllowed)
        {
            schema.AdditionalPropertiesAllowed = false;
            schema.AdditionalProperties = null;
            return;
        }

        var valueSchema = BuildAdditionalPropertiesSchema(attr);
        if (valueSchema is null)
        {
            // Fallback: open dictionary, no typed schema
            schema.AdditionalPropertiesAllowed = true;
            schema.AdditionalProperties = null;
            return;
        }

        schema.AdditionalPropertiesAllowed = true;
        schema.AdditionalProperties = valueSchema;
    }

    /// <summary>
    /// Builds the schema for additionalProperties based on the attribute settings.
    /// </summary>
    /// <param name="attr"> The OpenApiSchemaComponent attribute containing additionalProperties settings.</param>
    /// <returns>The constructed OpenApiSchema for additionalProperties, or null if not applicable.</returns>
    private OpenApiSchema? BuildAdditionalPropertiesSchema(OpenApiSchemaComponent attr)
    {
        OpenApiSchema? valueSchema = null;

        // 1) CLR type: use PrimitiveSchemaMap first, then InferPrimitiveSchema
        if (attr.AdditionalPropertiesClrType is not null)
        {
            var t = attr.AdditionalPropertiesClrType;

            if (PrimitiveSchemaMap.TryGetValue(t, out var factory))
            {
                valueSchema = factory();
            }
            else
            {
                // your existing inference logic (handles PowerShell types etc.)
                valueSchema = (OpenApiSchema?)InferPrimitiveSchema(t, requestBodyPreferred: false);
            }
        }
        // 2) Raw type/format string: small inline mapping, no ToJsonSchemaType helper
        else if (attr.AdditionalPropertiesType is not null)
        {
            JsonSchemaType? typeEnum = null;
            switch (attr.AdditionalPropertiesType.ToString().ToLowerInvariant())
            {
                case "string":
                    typeEnum = JsonSchemaType.String;
                    break;
                case "integer":
                    typeEnum = JsonSchemaType.Integer;
                    break;
                case "number":
                    typeEnum = JsonSchemaType.Number;
                    break;
                case "boolean":
                    typeEnum = JsonSchemaType.Boolean;
                    break;
                case "array":
                    typeEnum = JsonSchemaType.Array;
                    break;
                case "object":
                    typeEnum = JsonSchemaType.Object;
                    break;
                case "null":
                    typeEnum = JsonSchemaType.Null;
                    break;
            }

            if (typeEnum is not null)
            {
                valueSchema = new OpenApiSchema
                {
                    Type = typeEnum,
                    Format = attr.AdditionalPropertiesFormat
                };
            }
        }
        // 3) Ref: your OpenApiSchema doesn’t support $ref directly,
        // so for now we can’t express this cleanly. You can later add
        // a custom extension if you want.
        else if (!string.IsNullOrWhiteSpace(attr.AdditionalPropertiesRef))
        {
            // TODO: log / ignore / custom extension
            valueSchema = null;
        }

        if (valueSchema is null)
        {
            return null;
        }

        // Wrap as array if requested
        if (attr.AdditionalPropertiesIsArray)
        {
            valueSchema = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = valueSchema
            };
        }

        if (!string.IsNullOrWhiteSpace(attr.AdditionalPropertiesDescription))
        {
            valueSchema.Description = attr.AdditionalPropertiesDescription;
        }

        // attr.AdditionalPropertiesEnum: you can wire this up later if your
        // OpenApiSchema exposes some Enum collection; for now we skip it.

        return valueSchema;
    }
}
