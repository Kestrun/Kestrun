using System.Reflection;
using System.Text.Json.Nodes;
using Kestrun.Forms;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

public partial class OpenApiDocDescriptor
{
    private readonly Stack<string?> _formPartScopeStack = new();

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
        var (propertyType, allowNull) = UnwrapNullableType(p.PropertyType);

        if (propertyType == typeof(KrFilePart))
        {
            return BuildFilePartSchema(p, allowNull);
        }

        ApplyKrPartScope(p, propertyType, out var pushScope);

        IOpenApiSchema schema;
        try
        {
            schema = BuildPropertyTypeSchema(propertyType, p, built);
        }
        finally
        {
            if (pushScope)
            {
                _ = _formPartScopeStack.Pop();
            }
        }

        schema = ApplyNullableSchema(schema, allowNull);
        ApplySchemaAttr(MergeXmlAttributes(p), schema);
        PowerShellAttributes.ApplyPowerShellAttributes(p, schema);
        return schema;
    }

    /// <summary>
    /// Unwraps nullable types and returns the underlying type and nullable flag.
    /// </summary>
    /// <param name="propertyType">The original property type.</param>
    /// <returns>A tuple containing the non-nullable type and a nullable flag.</returns>
    private static (Type PropertyType, bool AllowNull) UnwrapNullableType(Type propertyType)
    {
        var underlying = Nullable.GetUnderlyingType(propertyType);
        return underlying is null ? (propertyType, false) : (underlying, true);
    }

    /// <summary>
    /// Builds the schema for a <see cref="KrFilePart"/> property, including nullability when needed.
    /// </summary>
    /// <param name="p">The property info.</param>
    /// <param name="allowNull">Whether the property allows null.</param>
    /// <returns>The constructed OpenAPI schema for the file part.</returns>
    private IOpenApiSchema BuildFilePartSchema(PropertyInfo p, bool allowNull)
    {
        var fileSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "binary"
        };
        ApplySchemaAttr(MergeXmlAttributes(p), fileSchema);
        PowerShellAttributes.ApplyPowerShellAttributes(p, fileSchema);
        return allowNull ? MakeNullable(fileSchema, isNullable: true) : fileSchema;
    }

    /// <summary>
    /// Applies form part attributes and pushes nested scope when required.
    /// </summary>
    /// <param name="p">The property info.</param>
    /// <param name="propertyType">The resolved property type.</param>
    /// <param name="pushScope">Set to <c>true</c> when a new scope was pushed.</param>
    private void ApplyKrPartScope(PropertyInfo p, Type propertyType, out bool pushScope)
    {
        var currentScope = _formPartScopeStack.Count > 0 ? _formPartScopeStack.Peek() : null;
        FormHelper.ApplyKrPartAttributes(Host, p, currentScope);

        var hasKrPartAttribute = p.IsDefined(typeof(KrPartAttribute), inherit: false);
        var partName = hasKrPartAttribute ? FormHelper.ResolvePartName(p) : null;
        pushScope = hasKrPartAttribute && !string.IsNullOrWhiteSpace(partName) && ShouldPushNestedScope(propertyType);
        if (pushScope)
        {
            _formPartScopeStack.Push(partName);
        }
    }

    /// <summary>
    /// Builds the schema for the resolved property type.
    /// </summary>
    /// <param name="propertyType">The resolved property type.</param>
    /// <param name="p">The property info.</param>
    /// <param name="built">The set of already built types to avoid recursion.</param>
    /// <returns>The constructed OpenAPI schema for the property type.</returns>
    private IOpenApiSchema BuildPropertyTypeSchema(Type propertyType, PropertyInfo p, HashSet<Type> built)
    {
        if (PrimitiveSchemaMap.TryGetValue(propertyType, out var getSchema))
        {
            return getSchema();
        }

        if (propertyType.IsArray)
        {
            return BuildArraySchema(propertyType, p, built);
        }

        // Treat enums and complex types the same: register as component and reference
        return BuildComplexTypeSchema(propertyType, p, built);
    }

    /// <summary>
    /// Applies nullable behavior to the schema when required.
    /// </summary>
    /// <param name="schema">The schema to update.</param>
    /// <param name="allowNull">Whether the property allows null.</param>
    /// <returns>The updated schema.</returns>
    private static IOpenApiSchema ApplyNullableSchema(IOpenApiSchema schema, bool allowNull)
    {
        if (!allowNull)
        {
            return schema;
        }

        if (schema is OpenApiSchema s)
        {
            // For inline schemas, add null type directly
            s.Type |= JsonSchemaType.Null;
            return s;
        }

        if (schema is OpenApiSchemaReference refSchema)
        {
            var modifiedRefSchema = refSchema.Clone();
            modifiedRefSchema.Description = null; // clear description to avoid duplication
            // For $ref schemas (enums/complex types), wrap in anyOf with null
            return new OpenApiSchema
            {
                AnyOf =
                [
                    modifiedRefSchema,
                    new OpenApiSchema { Type = JsonSchemaType.Null }
                ]
            };
        }

        return schema;
    }

    /// <summary>
    /// Determines whether to push a new nested scope based on the property type.
    /// </summary>
    /// <param name="propertyType">The type of the property to evaluate.</param>
    /// <returns><c>true</c> if a new nested scope should be pushed; otherwise, <c>false</c>.</returns>
    private static bool ShouldPushNestedScope(Type propertyType)
    {
        var candidate = propertyType;

        if (candidate.IsArray)
        {
            candidate = candidate.GetElementType()!;
        }

        if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            candidate = Nullable.GetUnderlyingType(candidate)!;
        }

        return !candidate.IsEnum && !PrimitiveSchemaMap.ContainsKey(candidate);
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
    private OpenApiSchema BuildEnumSchema(Type pt, PropertyInfo p)
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

        if (item == typeof(KrFilePart))
        {
            itemSchema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = "binary"
            };
        }
        else
        {
            if (PrimitiveSchemaMap.TryGetValue(item, out var getSchema))
            {
                itemSchema = getSchema();
            }
            else
            {
                // Treat enums and complex types the same: register as component and reference
                BuildSchema(item, built); // ensure component exists
                itemSchema = new OpenApiSchemaReference(item.Name);
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
