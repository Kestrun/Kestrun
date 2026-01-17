using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Merges OpenApiProperties with OpenApiXmlAttribute if present.
    /// </summary>
    /// <param name="prop">The property to extract attributes from.</param>
    /// <returns>Merged OpenApiProperties with XML metadata applied.</returns>
    private static OpenApiProperties? MergeXmlAttributes(PropertyInfo prop)
    {
        var properties = prop.GetCustomAttribute<OpenApiProperties>();
        var xmlAttr = prop.GetCustomAttribute<OpenApiXmlAttribute>();

        if (xmlAttr == null)
        {
            return properties;
        }

        // If no OpenApiProperties, create a new one to hold XML data
        properties ??= new OpenApiPropertyAttribute();

        // Merge XML attribute properties into OpenApiProperties
        if (!string.IsNullOrWhiteSpace(xmlAttr.Name))
        {
            properties.XmlName = xmlAttr.Name;
        }

        if (!string.IsNullOrWhiteSpace(xmlAttr.Namespace))
        {
            properties.XmlNamespace = xmlAttr.Namespace;
        }

        if (!string.IsNullOrWhiteSpace(xmlAttr.Prefix))
        {
            properties.XmlPrefix = xmlAttr.Prefix;
        }

        if (xmlAttr.Attribute)
        {
            properties.XmlAttribute = true;
        }

        if (xmlAttr.Wrapped)
        {
            properties.XmlWrapped = true;
        }

        return properties;
    }

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
#pragma warning disable IDE0045
        // Convert to conditional expression
        if (PrimitiveSchemaMap.TryGetValue(pt, out var getSchema))
        {
            schema = getSchema();
        }
        else if (pt.IsEnum)
        {
            schema = BuildEnumSchema(pt, p);
        }
        else
        {
            schema = pt.IsArray
                ? BuildArraySchema(pt, p, built)
                // Complex type
                : BuildComplexTypeSchema(pt, p, built);
        }
#pragma warning restore IDE0045
        // Convert to conditional expression
        // Apply nullable flag if needed
        if (allowNull && schema is OpenApiSchema s)
        {
            s.Type |= JsonSchemaType.Null;
        }
        ApplySchemaAttr(MergeXmlAttributes(p), schema);
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
        ApplySchemaAttr(MergeXmlAttributes(p), refSchema);
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
        ApplySchemaAttr(MergeXmlAttributes(p) ?? a, s);
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
        ApplySchemaAttr(MergeXmlAttributes(p), s);
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
        ApplySchemaAttr(MergeXmlAttributes(p), prim);
        PowerShellAttributes.ApplyPowerShellAttributes(p, prim);
        return prim;
    }

    /// <summary>
    /// Gets or creates an OpenAPI schema item in either inline or document components.
    /// </summary>
    /// <param name="schemaName">The name of the schema.</param>
    /// <param name="inline">Whether to use inline components or document components.</param>
    /// <returns>The OpenApiSchema item.</returns>
    private OpenApiSchema GetOrCreateSchemaItem(string schemaName, bool inline)
    {
        IDictionary<string, IOpenApiSchema> schema;
        // Determine whether to use inline components or document components
        if (inline)
        {
            // Use inline components
            InlineComponents.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
            schema = InlineComponents.Schemas;
        }
        else
        {
            // Use document components
            Document.Components ??= new OpenApiComponents();
            Document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
            schema = Document.Components.Schemas;
        }
        // Retrieve or create the request body item
        if (!schema.TryGetValue(schemaName, out var openApiSchemaItem) || openApiSchemaItem is null)
        {
            // Create a new OpenApiSchema if it doesn't exist
            openApiSchemaItem = new OpenApiSchema();
            schema[schemaName] = openApiSchemaItem;
        }
        // return the request body item
        return (OpenApiSchema)openApiSchemaItem;
    }

    /// <summary>
    /// Tries to get a schema by name from either inline or document components.
    /// </summary>
    /// <param name="schemaName">The name of the schema to retrieve.</param>
    /// <param name="schema">The retrieved schema if found; otherwise, null.</param>
    /// <param name="isInline">Indicates whether the schema was found in inline components.</param>
    /// <returns>True if the schema was found; otherwise, false.</returns>
    private bool TryGetSchemaItem(string schemaName, out OpenApiSchema? schema, out bool isInline)
    {
        if (TryGetInline(name: schemaName, kind: OpenApiComponentKind.Schemas, out schema))
        {
            isInline = true;
            return true;
        }
        else if (TryGetComponent(name: schemaName, kind: OpenApiComponentKind.Schemas, out schema))
        {
            isInline = false;
            return true;
        }
        schema = null;
        isInline = false;
        return false;
    }

    /// <summary>
    /// Tries to get a schema by name from either inline or document components.
    /// </summary>
    /// <param name="schemaName">The name of the schema to retrieve.</param>
    /// <param name="schema">The retrieved schema if found; otherwise, null.</param>
    /// <returns>True if the schema was found; otherwise, false.</returns>
    private bool TryGetSchemaItem(string schemaName, out OpenApiSchema? schema) =>
    TryGetSchemaItem(schemaName, out schema, out _);
}

