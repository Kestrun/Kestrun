using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Builds and adds the schema for a given type to the document components.
    /// </summary>
    /// <param name="t">The type to build the schema for.</param>
    /// <param name="built">The set of already built types to avoid recursion.</param>
    private void BuildSchema(Type t, HashSet<Type>? built = null)
    {
        if (Document.Components is not null && Document.Components.Schemas is not null)
        {
            if (!ComponentSchemasExists(t.Name))
            {
                if (!PrimitiveSchemaMap.ContainsKey(t))
                {
                    Document.Components.Schemas[t.Name] = BuildSchemaForType(t, built);
                }
            }
        }
    }

    /// <summary>
    /// Builds the schema for a property, handling nullable types and complex types.
    /// </summary>
    /// <param name="p">The property info.</param>
    /// <param name="built">The set of already built types to avoid recursion.</param>
    /// <returns>The constructed OpenAPI schema for the property.</returns>
    private IOpenApiSchema BuildPropertySchema(PropertyInfo p, HashSet<Type> built)
    {
        var pt = p.PropertyType;
        var allowNull = false;
        var underlying = Nullable.GetUnderlyingType(pt);
        if (underlying != null)
        {
            allowNull = true;
            pt = underlying;
        }
        IOpenApiSchema schema;
        if (PrimitiveSchemaMap.TryGetValue(pt, out var getSchema))
        {
            schema = getSchema();
        }
        else if (pt.IsEnum)
        {
            schema = BuildEnumSchema(pt, p);
        }
        else if (pt.IsArray)
        {
            schema = BuildArraySchema(pt, p, built);
        }
        else
        {
            schema = BuildComplexTypeSchema(pt, p, built);
        }
        // Apply nullable flag if needed
        if (allowNull && schema is OpenApiSchema s)
        {
            s.Type |= JsonSchemaType.Null;
        }
        ApplySchemaAttr(p.GetCustomAttribute<OpenApiProperties>(), schema);
        PowerShellAttributes.ApplyPowerShellAttributes(p, schema);
        return schema;
    }

    /// <summary>
    /// Builds the schema for a complex type property.
    /// </summary>
    /// <param name="pt">The property type.</param>
    /// <param name="p">The property info.</param>
    /// <param name="built">The set of already built types to avoid recursion.</param>
    /// <returns>The constructed OpenAPI schema for the complex type property.</returns>
    private OpenApiSchemaReference BuildComplexTypeSchema(Type pt, PropertyInfo p, HashSet<Type> built)
    {
        BuildSchema(pt, built); // ensure component exists
        var refSchema = new OpenApiSchemaReference(pt.Name);
        ApplySchemaAttr(p.GetCustomAttribute<OpenApiProperties>(), refSchema);
        return refSchema;
    }

    /// <summary>
    /// Builds the schema for an enum property.
    /// </summary>
    /// <param name="pt">The property type.</param>
    /// <param name="p">The property info.</param>
    /// <returns>The constructed OpenAPI schema for the enum property.</returns>
    private static OpenApiSchema BuildEnumSchema(Type pt, PropertyInfo p)
    {
        var s = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = [.. pt.GetEnumNames().Select(n => (JsonNode)n)]
        };
        var attrs = p.GetCustomAttributes<OpenApiPropertyAttribute>(inherit: false).ToArray();
        var a = MergeSchemaAttributes(attrs);
        ApplySchemaAttr(a, s);
        PowerShellAttributes.ApplyPowerShellAttributes(p, s);
        return s;
    }

    /// <summary>
    /// Builds the schema for an array property.
    /// </summary>
    /// <param name="pt">The property type.</param>
    /// <param name="p">The property info.</param>
    /// <param name="built">The set of already built types to avoid recursion.</param>
    /// <returns>The constructed OpenAPI schema for the array property.</returns>
    private OpenApiSchema BuildArraySchema(Type pt, PropertyInfo p, HashSet<Type> built)
    {
        var item = pt.GetElementType()!;
        IOpenApiSchema itemSchema;

        if (PrimitiveSchemaMap.TryGetValue(item, out var getSchema))
        {
            itemSchema = getSchema();
        }
        else
        {
            if (!item.IsEnum)
            {
                BuildSchema(item, built); // ensure component exists
                itemSchema = new OpenApiSchemaReference(item.Name);
            }
            else
            {
                itemSchema = BuildEnumSchema(item, p);
            }
        }
        var s = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = itemSchema
        };
        ApplySchemaAttr(p.GetCustomAttribute<OpenApiProperties>(), s);
        PowerShellAttributes.ApplyPowerShellAttributes(p, s);
        return s;
    }

    /// <summary>
    /// Builds the schema for a primitive type property.
    /// </summary>
    /// <param name="pt">The property type.</param>
    /// <param name="p">The property info.</param>
    /// <returns>The constructed OpenAPI schema for the primitive type property.</returns>
    private IOpenApiSchema BuildPrimitiveSchema(Type pt, PropertyInfo p)
    {
        var prim = InferPrimitiveSchema(pt);
        ApplySchemaAttr(p.GetCustomAttribute<OpenApiProperties>(), prim);
        PowerShellAttributes.ApplyPowerShellAttributes(p, prim);
        return prim;
    }
}
