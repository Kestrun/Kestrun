using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Kestrun.Runtime;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
    #region Schemas
    private static OpenApiProperties? GetSchemaIdentity(Type t)
    {
        // Prefer OpenApiPropertyAttribute (it supports operation/property-level overrides),
        // but fall back to any OpenApiProperties-derived attribute (e.g., OpenApiSchemaComponent).
        var propAttrs = (OpenApiPropertyAttribute[])t.GetCustomAttributes(typeof(OpenApiPropertyAttribute), inherit: true);
        if (propAttrs.Length > 0)
        {
            return propAttrs[0];
        }

        // Note: OpenApiSchemaComponent is Inherited=false, so inherit:true won't climb.
        // We walk the base chain manually to allow schemas deriving from OpenApi* primitives
        // (or from a base schema component) to inherit the underlying identity.
        for (var current = t; current is not null && current != typeof(object); current = current.BaseType)
        {
            var schemaAttrs = current.GetCustomAttributes(inherit: false).OfType<OpenApiProperties>().ToArray();
            if (schemaAttrs.Length > 0)
            {
                return schemaAttrs[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Builds and returns the schema for a given type.
    /// </summary>
    /// <param name="t">Type to build schema for</param>
    /// <param name="built">Set of types already built to avoid recursion</param>
    /// <returns>OpenApiSchema representing the type</returns>
    private IOpenApiSchema BuildSchemaForType(Type t, HashSet<Type>? built = null)
    {
        built ??= [];

        if (TryBuildPrimitiveSchema(t, out var primitiveSchema))
        {
            ApplyTypeAttributes(t, primitiveSchema);
            return primitiveSchema;
        }

        if (TryBuildDerivedSchemaFromBaseType(t, built, out var derivedSchema, out var schemaParent))
        {
            return derivedSchema;
        }

        var schema = CreateSchemaForDeclaredProperties(t);

        if (built.Contains(t))
        {
            return schema;
        }

        _ = built.Add(t);

        ApplyTypeAttributes(t, schema);

        if (t.IsEnum)
        {
            return RegisterEnumSchema(t);
        }
        // Extensions
        ProcessExtensions(t, schema);
        // Properties
        ProcessTypeProperties(t, schema, built);
        // Return composed schema if applicable
        return ComposeWithParentSchema(schemaParent, schema);
    }

    /// <summary>
    /// Processes OpenAPI extensions defined on a type and adds them to the schema.
    /// </summary>
    /// <param name="t">The type being processed.</param>
    /// <param name="schema"></param>
    private static void ProcessExtensions(Type t, OpenApiSchema schema)
    {
        foreach (var attr in t.GetCustomAttributes<OpenApiExtensionAttribute>(inherit: false))
        {
            schema.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
            // Parse string into a JsonNode tree.
            var node = JsonNode.Parse(attr.Json);
            if (node is null)
            {
                continue;
            }
            schema.Extensions[attr.Name] = new JsonNodeExtension(node);
        }
    }

    /// <summary>
    /// Attempts to create a schema for types mapped as OpenAPI primitives/scalars.
    /// </summary>
    /// <param name="t">The CLR type to map.</param>
    /// <param name="schema">The created primitive schema.</param>
    /// <returns><c>true</c> if the type was mapped as a primitive/scalar; otherwise, <c>false</c>.</returns>
    private static bool TryBuildPrimitiveSchema(Type t, out OpenApiSchema schema)
    {
        if (PrimitiveSchemaMap.TryGetValue(t, out var getSchema))
        {
            schema = getSchema();
            return true;
        }

        schema = null!;
        return false;
    }

    /// <summary>
    /// Attempts to resolve schema generation for types that derive from another schema component.
    /// This covers array-wrappers and inheritance composition via <c>allOf</c>.
    /// </summary>
    /// <param name="t">The derived type being processed.</param>
    /// <param name="built">The recursion guard set passed through schema-building.</param>
    /// <param name="resolved">The resolved schema to return if handled.</param>
    /// <param name="schemaParent">The parent schema, if composition should be applied later.</param>
    /// <returns><c>true</c> if schema generation was fully handled and <paramref name="resolved"/> is set.</returns>
    private bool TryBuildDerivedSchemaFromBaseType(
        Type t,
        HashSet<Type> built,
        out IOpenApiSchema resolved,
        out OpenApiSchema? schemaParent)
    {
        resolved = null!;
        schemaParent = null;

        if (!HasComposableBaseType(t))
        {
            return false;
        }

        var baseSchema = BuildBaseTypeSchema(t);
        if (baseSchema is null)
        {
            return false;
        }

        if (TryResolveSimpleOrReferenceBaseSchema(t, baseSchema, out resolved))
        {
            return true;
        }

        if (baseSchema is OpenApiSchema arraySchema && TryResolveArrayWrapperDerivedSchema(t, built, arraySchema, out resolved))
        {
            return true;
        }

        // Defer composition until after properties are processed.
        schemaParent = baseSchema as OpenApiSchema;
        return false;
    }

    /// <summary>
    /// Determines whether a type has a base type that can participate in schema composition.
    /// </summary>
    /// <param name="t">The type being processed.</param>
    /// <returns><c>true</c> if the type derives from something other than <see cref="object"/>; otherwise <c>false</c>.</returns>
    private static bool HasComposableBaseType(Type t)
        => t.BaseType is not null && t.BaseType != typeof(object);

    /// <summary>
    /// Attempts to resolve a derived type immediately when its base schema is a simple schema or a reference.
    /// </summary>
    /// <param name="derivedType">The derived type being processed.</param>
    /// <param name="baseSchema">The schema resolved from the base type.</param>
    /// <param name="resolved">The resolved schema to return.</param>
    /// <returns><c>true</c> if the schema was resolved immediately; otherwise <c>false</c>.</returns>
    private bool TryResolveSimpleOrReferenceBaseSchema(
        Type derivedType,
        IOpenApiSchema baseSchema,
        out IOpenApiSchema resolved)
    {
        resolved = null!;

        if (!IsSimpleSchemaOrReference(baseSchema))
        {
            return false;
        }

        if (baseSchema is OpenApiSchema openApiSchema)
        {
            ApplyTypeAttributes(derivedType, openApiSchema);
            resolved = openApiSchema;
            return true;
        }

        resolved = baseSchema;
        return true;
    }

    /// <summary>
    /// Determines whether a schema represents a "simple" base schema (not composed via <c>allOf</c>)
    /// or a schema reference.
    /// </summary>
    /// <param name="schema">The schema to check.</param>
    /// <returns><c>true</c> if the schema is simple or a reference; otherwise <c>false</c>.</returns>
    private static bool IsSimpleSchemaOrReference(IOpenApiSchema schema)
    {
        return schema is OpenApiSchemaReference
            || (schema.AllOf is null && schema.Type != JsonSchemaType.Array);
    }

    /// <summary>
    /// Resolves the special case where a derived type represents an array wrapper and the array items
    /// may themselves be composed via <c>allOf</c>.
    /// </summary>
    /// <param name="derivedType">The derived type being processed.</param>
    /// <param name="built">The recursion guard set passed through schema-building.</param>
    /// <param name="arraySchema">The schema resolved from the base type.</param>
    /// <param name="resolved">The resolved schema to return.</param>
    /// <returns><c>true</c> if handled; otherwise <c>false</c>.</returns>
    private bool TryResolveArrayWrapperDerivedSchema(
        Type derivedType,
        HashSet<Type> built,
        OpenApiSchema arraySchema,
        out IOpenApiSchema resolved)
    {
        resolved = null!;

        if (arraySchema.Type != JsonSchemaType.Array || arraySchema.Items is null)
        {
            return false;
        }

        ApplyTypeAttributes(derivedType, arraySchema);

        if (arraySchema.Items is not OpenApiSchema itemSchema)
        {
            resolved = arraySchema;
            return true;
        }

        // Preserve existing behavior (type-level attributes applied twice in this branch).
        ApplyTypeAttributes(derivedType, arraySchema);

        if (itemSchema.AllOf is null)
        {
            resolved = arraySchema;
            return true;
        }

        var additional = CreateAllOfAdditionalObjectSchema(derivedType, built);
        itemSchema.AllOf.Add(additional);
        resolved = arraySchema;
        return true;
    }

    /// <summary>
    /// Creates the additional object schema appended to an existing <c>allOf</c> list for a derived type.
    /// </summary>
    /// <param name="t">The derived type whose properties should be added.</param>
    /// <param name="built">The recursion guard set passed through schema-building.</param>
    /// <returns>An <see cref="OpenApiSchema"/> containing only properties declared on <paramref name="t"/>.</returns>
    private OpenApiSchema CreateAllOfAdditionalObjectSchema(Type t, HashSet<Type> built)
    {
        var additional = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        };

        ProcessTypeProperties(t, additional, built);
        return additional;
    }

    /// <summary>
    /// Creates the base schema instance for a type, initializing object properties only when
    /// the type declares at least one public instance property.
    /// </summary>
    /// <param name="t">The CLR type being processed.</param>
    /// <returns>An <see cref="OpenApiSchema"/> ready for property population.</returns>
    private static OpenApiSchema CreateSchemaForDeclaredProperties(Type t)
    {
        var schema = new OpenApiSchema();
        var declaredPropsCount =
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Count(p => p.DeclaringType == t);

        if (declaredPropsCount > 0)
        {
            schema.Type = JsonSchemaType.Object;
            schema.Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        }

        return schema;
    }

    /// <summary>
    /// Composes a child schema into a parent schema when inheritance composition is active.
    /// </summary>
    /// <param name="schemaParent">The parent schema built from the base type (if any).</param>
    /// <param name="schema">The derived schema with properties populated.</param>
    /// <returns>The composed schema to return from schema generation.</returns>
    private static IOpenApiSchema ComposeWithParentSchema(OpenApiSchema? schemaParent, OpenApiSchema schema)
    {
        if (schemaParent is null)
        {
            return schema;
        }

        if (schemaParent.AllOf is not null)
        {
            schemaParent.AllOf.Add(schema);
            schemaParent.Type = null; // Clear type when using allOf
            return schemaParent;
        }

        if (schemaParent.Type == JsonSchemaType.Array)
        {
            if (schemaParent.Items is OpenApiSchema items && items.AllOf is not null)
            {
                items.AllOf.Add(schema);
            }

            return schemaParent;
        }

        return schema;
    }

    /// <summary>
    /// Builds schema for custom base type derivations.
    /// </summary>
    ///  <param name="t">Type to build schema for</param>
    /// <returns>OpenApiSchema representing the base type derivation, or null if not applicable</returns>
    private static IOpenApiSchema? BuildBaseTypeSchema(Type t)
    {
        if (PrimitiveSchemaMap.TryGetValue(t.BaseType!, out var value))
        {
            return value();
        }

        // Fallback to custom base type schema building
        return BuildCustomBaseTypeSchema(t);
    }

    /// <summary>
    /// Builds schema for types with custom base types.
    /// </summary>
    /// <param name="t">Type to build schema for</param>
    /// <returns>OpenApiSchema representing the custom base type derivation</returns>
    private static IOpenApiSchema BuildCustomBaseTypeSchema(Type t)
    {
        var attributes = t.CustomAttributes.ToArray();
        // Count declared properties
        var declaredPropsCount =
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Count(p => p.DeclaringType == t);

        // Check for the special case where the derived type only adds array semantics
        var hasArray =
            attributes.Length > 0 &&
            attributes[0].NamedArguments.Any(na =>
                na.MemberName == "Array" &&
                na.TypedValue.ArgumentType == typeof(bool) &&
                na.TypedValue.Value is bool b && b);
        // If so, we can represent this as a simple reference to the base type
        if (declaredPropsCount == 0 && attributes.Length == 1)
        {
            if (hasArray)
            {
                return new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchemaReference(t.BaseType!.Name)
                };
            }
            // If the derived type has AdditionalProperties, we can't use allOf
            return new OpenApiSchemaReference(t.BaseType!.Name);
        }
        // Otherwise, build an allOf schema referencing the base type
        var schema = new OpenApiSchema
        {
            AllOf = [new OpenApiSchemaReference(t.BaseType!.Name)]
        };
        // Apply array semantics if specified
        return hasArray
            ? new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = schema
            }
            : schema;
    }

    /// <summary>
    /// Registers an enum type schema in the document components.
    /// </summary>
    /// <returns>The registered enum schema.</returns>
    private OpenApiSchema RegisterEnumSchema(Type enumType)
    {
        var enumSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = [.. enumType.GetEnumNames().Select(n => (JsonNode)n)]
        };
        
        if (Document.Components?.Schemas is not null)
        {
            Document.Components.Schemas[enumType.Name] = enumSchema;
        }
        
        return enumSchema;
    }

    /// <summary>
    /// Applies type-level attributes to a schema.
    /// </summary>
    private static void ApplyTypeAttributes(Type t, OpenApiSchema schema)
    {
        foreach (var attr in t.GetCustomAttributes(true)
          .Where(a => a is OpenApiPropertyAttribute or OpenApiSchemaComponent))
        {
            ApplySchemaAttr(attr as OpenApiProperties, schema);

            if (attr is OpenApiSchemaComponent schemaAttribute && schemaAttribute.Examples is not null)
            {
                schema.Examples ??= [];
                var node = OpenApiJsonNodeFactory.ToNode(schemaAttribute.Examples);
                if (node is not null)
                {
                    schema.Examples.Add(node);
                }
            }
        }
    }

    /// <summary>
    /// Processes all properties of a type and builds their schemas.
    /// </summary>
    private void ProcessTypeProperties(Type t, OpenApiSchema schema, HashSet<Type> built)
    {
        var instance = TryCreateTypeInstance(t);

        foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                      .Where(p => p.DeclaringType == t))
        {
            var propSchema = BuildPropertySchema(prop, built);
            CapturePropertyDefault(instance, prop, propSchema);

            if (prop.GetCustomAttribute<OpenApiAdditionalPropertiesAttribute>() is not null)
            {
                schema.AdditionalPropertiesAllowed = true;
                schema.AdditionalProperties = propSchema;
            }
            else
            {
                schema.Properties?.Add(prop.Name, propSchema);
            }
        }
    }

    /// <summary>
    /// Attempts to create an instance of a type to capture default values.
    /// </summary>
    private static object? TryCreateTypeInstance(Type t)
    {
        try
        {
            return Activator.CreateInstance(t);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Captures the default value of a property if not already set.
    /// </summary>
    private static void CapturePropertyDefault(object? instance, PropertyInfo prop, IOpenApiSchema propSchema)
    {
        if (instance is null || propSchema is not OpenApiSchema concrete || concrete.Default is not null)
        {
            return;
        }

        try
        {
            var value = prop.GetValue(instance);
            if (!IsIntrinsicDefault(value, prop.PropertyType))
            {
                concrete.Default = OpenApiJsonNodeFactory.ToNode(value);
            }
        }
        catch
        {
            // Ignore failures when capturing defaults
        }
    }

    /// <summary>
    /// Determines if a value is the intrinsic default for its declared type.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="declaredType">The declared type of the value.</param>
    /// <returns>True if the value is the intrinsic default for its declared type; otherwise, false.</returns>
    private static bool IsIntrinsicDefault(object? value, Type declaredType)
    {
        if (value is null)
        {
            return true;
        }

        // Unwrap Nullable<T>
        var t = Nullable.GetUnderlyingType(declaredType) ?? declaredType;

        // Reference types: null is the only intrinsic default
        if (!t.IsValueType)
        {
            return false;
        }

        // Special-cases for common structs
        if (t == typeof(Guid))
        {
            return value.Equals(Guid.Empty);
        }

        if (t == typeof(TimeSpan))
        {
            return value.Equals(TimeSpan.Zero);
        }

        if (t == typeof(DateTime))
        {
            return value.Equals(default(DateTime));
        }

        if (t == typeof(DateTimeOffset))
        {
            return value.Equals(default(DateTimeOffset));
        }

        // Enums: 0 is intrinsic default
        if (t.IsEnum)
        {
            return Convert.ToInt64(value) == 0;
        }

        // Primitive/value types: compare to default(T)
        var def = Activator.CreateInstance(t);
        return value.Equals(def);
    }

    /// <summary>
    /// Makes an OpenApiSchema nullable if specified.
    /// </summary>
    /// <param name="schema">The OpenApiSchema to modify.</param>
    /// <param name="isNullable">Indicates whether the schema should be nullable.</param>
    /// <returns>The modified OpenApiSchema.</returns>
    private static OpenApiSchema MakeNullable(OpenApiSchema schema, bool isNullable)
    {
        if (isNullable)
        {
            schema.Type |= JsonSchemaType.Null;
        }
        return schema;
    }

    /// <summary>
    /// Infers a primitive OpenApiSchema from a .NET type.
    /// </summary>
    /// <param name="type">The .NET type to infer from.</param>
    /// <param name="inline">Indicates if the schema should be inlined.</param>
    /// <returns>The inferred OpenApiSchema.</returns>
    public IOpenApiSchema InferPrimitiveSchema(Type type, bool inline = false)
    {
        var nullable = false;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)
            && type.GetGenericArguments().Length == 1)
        {
            type = type.GetGenericArguments()[0];
            nullable = true;
        }
        // Direct type mappings
        if (PrimitiveSchemaMap.TryGetValue(type, out var schemaFactory))
        {
            return MakeNullable(schemaFactory(), nullable);
        }

        // Array type handling
        if (type.Name.EndsWith("[]"))
        {
            return InferArraySchema(type, inline);
        }

        // Special handling for PowerShell OpenAPI classes
        if (PowerShellOpenApiClassExporter.ValidClassNames.Contains(type.Name))
        {
            return InferPowerShellClassSchema(type, inline);
        }

        // Fallback
        return new OpenApiSchema { Type = JsonSchemaType.String };
    }

    /// <summary>
    /// Infers an array OpenApiSchema from a .NET array type.
    /// </summary>
    /// <param name="type">The .NET array type to infer from.</param>
    /// <param name="inline">Indicates if the schema should be inlined.</param>
    /// <returns>The inferred OpenApiSchema.</returns>
    private OpenApiSchema InferArraySchema(Type type, bool inline)
    {
        var typeName = type.Name[..^2];
        if (ComponentSchemasExists(typeName))
        {
            var items = inline ? GetSchema(typeName).Clone() : new OpenApiSchemaReference(typeName);
            return new OpenApiSchema { Type = JsonSchemaType.Array, Items = items };
        }

        return new OpenApiSchema { Type = JsonSchemaType.Array, Items = InferPrimitiveSchema(type.GetElementType() ?? typeof(object)) };
    }

    /// <summary>
    /// Infers a PowerShell OpenAPI class schema.
    /// </summary>
    /// <param name="type">The .NET type representing the PowerShell OpenAPI class.</param>
    /// <param name="inline">Indicates if the schema should be inlined.</param>
    /// <returns>The inferred OpenApiSchema.</returns>
    private IOpenApiSchema InferPowerShellClassSchema(Type type, bool inline)
    {
        if (TryGetSchemaItem(type.Name, out var schema, out var isInline))
        {
            if (inline || isInline)
            {
                if (schema is OpenApiSchema concreteSchema)
                {
                    return concreteSchema.Clone();
                }
            }
            else
            {
                return new OpenApiSchemaReference(type.Name);
            }
        }

        Host.Logger.Warning("Schema for PowerShell OpenAPI class '{typeName}' not found. Defaulting to string schema.", type.Name);
        return new OpenApiSchema { Type = JsonSchemaType.String };
    }

    /// <summary>
    /// Mapping of .NET primitive types to OpenAPI schema definitions.
    /// </summary>
    /// <remarks>
    /// This dictionary maps common .NET primitive types to their corresponding OpenAPI schema representations.
    /// Each entry consists of a .NET type as the key and a function that returns an OpenApiSchema as the value.
    /// </remarks>
    private static readonly Dictionary<Type, Func<OpenApiSchema>> PrimitiveSchemaMap = new()
    {
        [typeof(string)] = () => new OpenApiSchema { Type = JsonSchemaType.String },
        [typeof(bool)] = () => new OpenApiSchema { Type = JsonSchemaType.Boolean },
        [typeof(long)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" },
        [typeof(DateTime)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
        [typeof(DateTimeOffset)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
        [typeof(TimeSpan)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "duration" },
        [typeof(byte[])] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
        [typeof(Uri)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" },
        [typeof(Guid)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
        [typeof(object)] = () => new OpenApiSchema { Type = JsonSchemaType.Object },
        [typeof(void)] = () => new OpenApiSchema { Type = JsonSchemaType.Null },
        [typeof(char)] = () => new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 1, MinLength = 1 },
        [typeof(sbyte)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(byte)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(short)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(ushort)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(int)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(uint)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(long)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" },
        [typeof(ulong)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" },
        [typeof(float)] = () => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "float" },
        [typeof(double)] = () => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "double" },
        [typeof(decimal)] = () => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "decimal" },
        [typeof(OpenApiString)] = () => new OpenApiSchema { Type = JsonSchemaType.String },
        [typeof(OpenApiUuid)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
        [typeof(OpenApiDate)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" },
        [typeof(OpenApiDateTime)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
        [typeof(OpenApiEmail)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "email" },
        [typeof(OpenApiBinary)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
        [typeof(OpenApiHostname)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "hostname" },
        [typeof(OpenApiIpv4)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "ipv4" },
        [typeof(OpenApiIpv6)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "ipv6" },
        [typeof(OpenApiUri)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" },
        [typeof(OpenApiUrl)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "url" },
        [typeof(OpenApiByte)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "byte" },
        [typeof(OpenApiPassword)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "password" },
        [typeof(OpenApiRegex)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "regex" },
        [typeof(OpenApiJson)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "json" },
        [typeof(OpenApiXml)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "xml" },
        [typeof(OpenApiYaml)] = () => new OpenApiSchema { Type = JsonSchemaType.String, Format = "yaml" },

        [typeof(OpenApiInteger)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer },
        [typeof(OpenApiInt32)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
        [typeof(OpenApiInt64)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" },

        [typeof(OpenApiNumber)] = () => new OpenApiSchema { Type = JsonSchemaType.Number },
        [typeof(OpenApiFloat)] = () => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "float" },
        [typeof(OpenApiDouble)] = () => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "double" },

        [typeof(OpenApiBoolean)] = () => new OpenApiSchema { Type = JsonSchemaType.Boolean, Format = "boolean" },
    };

    /// <summary>
    /// Applies schema attributes to an OpenAPI schema.
    /// </summary>
    /// <param name="oaProperties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="ioaSchema">The OpenAPI schema to apply attributes to.</param>
    private static void ApplySchemaAttr(OpenApiProperties? oaProperties, IOpenApiSchema ioaSchema)
    {
        if (oaProperties is null)
        {
            return;
        }

        // Most models implement OpenApiSchema (concrete) OR OpenApiSchemaReference.
        // We set common metadata when possible (Description/Title apply only to concrete schema).
        if (ioaSchema is OpenApiSchema concreteSchema)
        {
            ApplyConcreteSchemaAttributes(oaProperties, concreteSchema);
            return;
        }

        if (ioaSchema is OpenApiSchemaReference refSchema)
        {
            ApplyReferenceSchemaAttributes(oaProperties, refSchema);
        }
    }

    /// <summary>
    /// Applies concrete schema attributes to an OpenApiSchema.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="schema">The OpenApiSchema to apply attributes to.</param>
    private static void ApplyConcreteSchemaAttributes(OpenApiProperties properties, OpenApiSchema schema)
    {
        ApplyTitleAndDescription(properties, schema);
        ApplySchemaType(properties, schema);
        ApplyFormatAndNumericBounds(properties, schema);
        ApplyLengthAndPattern(properties, schema);
        ApplyCollectionConstraints(properties, schema);
        ApplyFlags(properties, schema);
        ApplyExamplesAndDefaults(properties, schema);
    }

    /// <summary>
    /// Applies title and description to an OpenApiSchema.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="schema">The OpenApiSchema to apply attributes to.</param>
    private static void ApplyTitleAndDescription(OpenApiProperties properties, OpenApiSchema schema)
    {
        if (properties.Title is not null)
        {
            schema.Title = properties.Title;
        }
        if (properties is not OpenApiParameterComponentAttribute && properties.Description is not null)
        {
            schema.Description = properties.Description;
        }
    }

    /// <summary>
    /// Applies schema type and nullability to an OpenApiSchema.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="schema">The OpenApiSchema to apply attributes to.</param>
    private static void ApplySchemaType(OpenApiProperties properties, OpenApiSchema schema)
    {
        if (properties.Type != OaSchemaType.None)
        {
            schema.Type = properties.Type switch
            {
                OaSchemaType.String => JsonSchemaType.String,
                OaSchemaType.Number => JsonSchemaType.Number,
                OaSchemaType.Integer => JsonSchemaType.Integer,
                OaSchemaType.Boolean => JsonSchemaType.Boolean,
                OaSchemaType.Array => JsonSchemaType.Array,
                OaSchemaType.Object => JsonSchemaType.Object,
                OaSchemaType.Null => JsonSchemaType.Null,
                _ => schema.Type
            };
        }

        if (properties.Nullable)
        {
            schema.Type |= JsonSchemaType.Null;
        }
    }

    /// <summary>
    /// Applies format and numeric bounds to an OpenApiSchema.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="schema"></param>
    private static void ApplyFormatAndNumericBounds(OpenApiProperties properties, OpenApiSchema schema)
    {
        if (!string.IsNullOrWhiteSpace(properties.Format))
        {
            schema.Format = properties.Format;
        }

        if (properties.MultipleOf.HasValue)
        {
            schema.MultipleOf = properties.MultipleOf;
        }

        if (!string.IsNullOrWhiteSpace(properties.Maximum))
        {
            schema.Maximum = properties.Maximum;
            if (properties.ExclusiveMaximum)
            {
                schema.ExclusiveMaximum = properties.Maximum;
            }
        }

        if (!string.IsNullOrWhiteSpace(properties.Minimum))
        {
            schema.Minimum = properties.Minimum;
            if (properties.ExclusiveMinimum)
            {
                schema.ExclusiveMinimum = properties.Minimum;
            }
        }
    }

    /// <summary>
    /// Applies length and pattern constraints to an OpenApiSchema.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="schema"></param>
    private static void ApplyLengthAndPattern(OpenApiProperties properties, OpenApiSchema schema)
    {
        if (properties.MaxLength >= 0)
        {
            schema.MaxLength = properties.MaxLength;
        }

        if (properties.MinLength >= 0)
        {
            schema.MinLength = properties.MinLength;
        }

        if (!string.IsNullOrWhiteSpace(properties.Pattern))
        {
            schema.Pattern = properties.Pattern;
        }
    }

    /// <summary>
    /// Applies collection constraints to an OpenApiSchema.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="schema">The OpenApiSchema to apply attributes to.</param>
    private static void ApplyCollectionConstraints(OpenApiProperties properties, OpenApiSchema schema)
    {
        if (properties.MaxItems >= 0)
        {
            schema.MaxItems = properties.MaxItems;
        }

        if (properties.MinItems >= 0)
        {
            schema.MinItems = properties.MinItems;
        }

        if (properties.UniqueItems)
        {
            schema.UniqueItems = true;
        }

        if (properties.MaxProperties >= 0)
        {
            schema.MaxProperties = properties.MaxProperties;
        }

        if (properties.MinProperties >= 0)
        {
            schema.MinProperties = properties.MinProperties;
        }
    }

    private static void ApplyFlags(OpenApiProperties properties, OpenApiSchema schema)
    {
        schema.ReadOnly = properties.ReadOnly;
        schema.WriteOnly = properties.WriteOnly;
        schema.AdditionalPropertiesAllowed = properties.AdditionalPropertiesAllowed;
        schema.UnevaluatedProperties = properties.UnevaluatedProperties;
        if (properties is not OpenApiParameterComponentAttribute)
        {
            schema.Deprecated = properties.Deprecated;
        }
    }

    private static void ApplyExamplesAndDefaults(OpenApiProperties properties, OpenApiSchema schema)
    {
        if (properties.Default is not null)
        {
            schema.Default = OpenApiJsonNodeFactory.ToNode(properties.Default);
        }
        if (properties.Example is not null && properties is not OpenApiParameterComponentAttribute)
        {
            schema.Example = OpenApiJsonNodeFactory.ToNode(properties.Example);
        }

        if (properties.Enum is { Length: > 0 })
        {
            schema.Enum = [.. properties.Enum.Select(OpenApiJsonNodeFactory.ToNode).OfType<JsonNode>()];
        }

        if (properties.RequiredProperties is { Length: > 0 })
        {
            schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in properties.RequiredProperties)
            {
                _ = schema.Required.Add(r);
            }
        }
    }

    /// <summary>
    /// Applies reference schema attributes to an OpenApiSchemaReference.
    /// </summary>
    /// <param name="properties">The OpenApiProperties containing attributes to apply.</param>
    /// <param name="reference">The OpenApiSchemaReference to apply attributes to.</param>
    private static void ApplyReferenceSchemaAttributes(OpenApiProperties properties, OpenApiSchemaReference reference)
    {
        // Description/Title can live on a reference proxy in v2 (and serialize alongside $ref)
        if (!string.IsNullOrWhiteSpace(properties.Description))
        {
            reference.Description = properties.Description;
        }

        if (!string.IsNullOrWhiteSpace(properties.Title))
        {
            reference.Title = properties.Title;
        }

        // Example/Default/Enum aren’t typically set on the ref node itself;
        // attach such metadata to the component target instead if you need it.
    }

    #endregion
}
