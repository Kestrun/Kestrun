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

        // Handle custom base type derivations first
        if (t.BaseType is not null && t.BaseType != typeof(object))
        {
            var baseTypeSchema = BuildBaseTypeSchema(t);
            if (baseTypeSchema is not null)
            {
                return baseTypeSchema;
            }
        }

        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        };

        // Prevent infinite recursion
        if (built.Contains(t))
        {
            return schema;
        }

        _ = built.Add(t);

        // Handle enum types
        if (t.IsEnum)
        {
            RegisterEnumSchema(t);
            return schema;
        }

        // Early return for primitive types
        if (IsPrimitiveLike(t))
        {
            return schema;
        }

        // Apply type-level attributes
        ApplyTypeAttributes(t, schema);

        // Process properties with default value capture
        ProcessTypeProperties(t, schema, built);

        return schema;
    }

    /// <summary>
    /// Builds schema for custom base type derivations.
    /// </summary>
    ///  <param name="t">Type to build schema for</param>
    /// <returns>OpenApiSchema representing the base type derivation, or null if not applicable</returns>
    private IOpenApiSchema? BuildBaseTypeSchema(Type t)
    {
        var primitivesAssembly = typeof(OpenApiString).Assembly;

        // Determine if the base type is a known OpenAPI primitive type
        OaSchemaType? baseTypeName = t.BaseType switch
        {
            Type bt when bt == typeof(OpenApiString) => OaSchemaType.String,
            Type bt when bt == typeof(OpenApiInteger) => OaSchemaType.Integer,
            Type bt when bt == typeof(OpenApiNumber) => OaSchemaType.Number,
            Type bt when bt == typeof(OpenApiBoolean) => OaSchemaType.Boolean,
            _ => null
        };

        // If a type derives from one of our OpenApi* primitives, treat it as that primitive schema
        // and then apply any attributes on the derived type.
        if (baseTypeName is not null)
        {
            return BuildCustomBaseTypeSchema(t, baseTypeName);
        }

        // Only treat built-in OpenApi* primitives (and their variants) as raw OpenApi types.
        // User-defined schema components (including array wrappers like EventDates : Date)
        // should fall through to BuildCustomBaseTypeSchema so we can emit $ref + array items.
        if (t.Assembly == primitivesAssembly && typeof(IOpenApiType).IsAssignableFrom(t))
        {
            return BuildOpenApiTypeSchema(t);
        }
        // Fallback to custom base type schema building
        return BuildCustomBaseTypeSchema(t, baseTypeName);
    }

    /// <summary>
    /// Builds schema for types implementing IOpenApiType.
    /// </summary>
    private static OpenApiSchema? BuildOpenApiTypeSchema(Type t)
    {
        var attr = GetSchemaIdentity(t);
        return attr is not null
            ? new OpenApiSchema
            {
                Type = attr.Type.ToJsonSchemaType(),
                Format = attr.Format
            }
            : null;
    }

    /// <summary>
    /// Builds schema for types with custom base types.
    /// </summary>
    private IOpenApiSchema BuildCustomBaseTypeSchema(Type t, OaSchemaType? baseTypeName)
    {
        IOpenApiSchema baseSchema = baseTypeName is not null
            ? new OpenApiSchema { Type = baseTypeName?.ToJsonSchemaType() }
            : new OpenApiSchemaReference(t.BaseType!.Name);

        var schemaComps = t.GetCustomAttributes<OpenApiProperties>()
            .Where(schemaComp => schemaComp is not null)
            .Cast<OpenApiProperties>();

        foreach (var prop in schemaComps)
        {
            return BuildPropertyFromAttribute(prop, baseSchema);
        }

        return baseSchema;
    }

    /// <summary>
    /// Builds a property schema from an OpenApiProperties attribute.
    /// </summary>
    private static IOpenApiSchema BuildPropertyFromAttribute(OpenApiProperties prop, IOpenApiSchema baseSchema)
    {
        var schema = prop.Array
            ? new OpenApiSchema { Type = JsonSchemaType.Array, Items = baseSchema }
            : baseSchema;

        ApplySchemaAttr(prop, schema);
        return schema;
    }

    /// <summary>
    /// Registers an enum type schema in the document components.
    /// </summary>
    private void RegisterEnumSchema(Type enumType)
    {
        if (Document.Components?.Schemas is not null)
        {
            Document.Components.Schemas[enumType.Name] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = [.. enumType.GetEnumNames().Select(n => (JsonNode)n)]
            };
        }
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
                var node = ToNode(schemaAttribute.Examples);
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

        foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
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
                concrete.Default = ToNode(value);
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
    /// Merges multiple OpenApiPropertyAttribute instances into one.
    /// </summary>
    /// <param name="attrs">An array of OpenApiPropertyAttribute instances to merge.</param>
    /// <returns>A single OpenApiPropertyAttribute instance representing the merged attributes.</returns>
    private static OpenApiPropertyAttribute? MergeSchemaAttributes(OpenApiPropertyAttribute[] attrs)
    {
        if (attrs == null || attrs.Length == 0)
        {
            return null;
        }

        if (attrs.Length == 1)
        {
            return attrs[0];
        }

        var m = new OpenApiPropertyAttribute();

        foreach (var a in attrs)
        {
            MergeStringProperties(m, a);
            MergeEnumAndCollections(m, a);
            MergeNumericProperties(m, a);
            MergeBooleanProperties(m, a);
            MergeTypeAndRequired(m, a);
            MergeCustomFields(m, a);
        }

        return m;
    }

    /// <summary>
    /// Merges string properties where the last non-empty value wins.
    /// </summary>
    /// <param name="merged">The merged OpenApiPropertyAttribute to update.</param>
    /// <param name="attr">The OpenApiPropertyAttribute to merge from.</param>
    private static void MergeStringProperties(OpenApiPropertyAttribute merged, OpenApiPropertyAttribute attr)
    {
        if (!string.IsNullOrWhiteSpace(attr.Title))
        {
            merged.Title = attr.Title;
        }

        if (!string.IsNullOrWhiteSpace(attr.Description))
        {
            merged.Description = attr.Description;
        }

        if (!string.IsNullOrWhiteSpace(attr.Format))
        {
            merged.Format = attr.Format;
        }

        if (!string.IsNullOrWhiteSpace(attr.Pattern))
        {
            merged.Pattern = attr.Pattern;
        }

        if (!string.IsNullOrWhiteSpace(attr.Maximum))
        {
            merged.Maximum = attr.Maximum;
        }

        if (!string.IsNullOrWhiteSpace(attr.Minimum))
        {
            merged.Minimum = attr.Minimum;
        }
    }

    /// <summary>
    /// Merges enum and collection properties.
    /// </summary>
    /// <param name="merged">The merged OpenApiPropertyAttribute to update.</param>
    /// <param name="attr">The OpenApiPropertyAttribute to merge from.</param>
    private static void MergeEnumAndCollections(OpenApiPropertyAttribute merged, OpenApiPropertyAttribute attr)
    {
        if (attr.Enum is { Length: > 0 })
        {
            merged.Enum = [.. merged.Enum ?? [], .. attr.Enum];
        }

        if (attr.Default is not null)
        {
            merged.Default = attr.Default;
        }

        if (attr.Example is not null)
        {
            merged.Example = attr.Example;
        }
    }

    /// <summary>
    /// Merges numeric properties where values >= 0 are considered explicitly set.
    /// </summary>
    /// <param name="merged">The merged OpenApiPropertyAttribute to update.</param>
    /// <param name="attr">The OpenApiPropertyAttribute to merge from.</param>
    private static void MergeNumericProperties(OpenApiPropertyAttribute merged, OpenApiPropertyAttribute attr)
    {
        if (attr.MaxLength >= 0)
        {
            merged.MaxLength = attr.MaxLength;
        }

        if (attr.MinLength >= 0)
        {
            merged.MinLength = attr.MinLength;
        }

        if (attr.MaxItems >= 0)
        {
            merged.MaxItems = attr.MaxItems;
        }

        if (attr.MinItems >= 0)
        {
            merged.MinItems = attr.MinItems;
        }

        if (attr.MultipleOf is not null)
        {
            merged.MultipleOf = attr.MultipleOf;
        }
    }

    /// <summary>
    /// Merges boolean properties using OR logic.
    /// </summary>
    /// <param name="merged">The merged OpenApiPropertyAttribute to update.</param>
    /// <param name="attr">The OpenApiPropertyAttribute to merge from.</param>
    private static void MergeBooleanProperties(OpenApiPropertyAttribute merged, OpenApiPropertyAttribute attr)
    {
        merged.Nullable |= attr.Nullable;
        merged.ReadOnly |= attr.ReadOnly;
        merged.WriteOnly |= attr.WriteOnly;
        merged.Deprecated |= attr.Deprecated;
        merged.UniqueItems |= attr.UniqueItems;
        merged.ExclusiveMaximum |= attr.ExclusiveMaximum;
        merged.ExclusiveMinimum |= attr.ExclusiveMinimum;
    }

    /// <summary>
    /// Merges type and required properties.
    /// </summary>
    /// <param name="merged">The merged OpenApiPropertyAttribute to update.</param>
    /// <param name="attr">The OpenApiPropertyAttribute to merge from.</param>
    private static void MergeTypeAndRequired(OpenApiPropertyAttribute merged, OpenApiPropertyAttribute attr)
    {
        if (attr.Type != OaSchemaType.None)
        {
            merged.Type = attr.Type;
        }

        if (attr.RequiredProperties is { Length: > 0 })
        {
            merged.RequiredProperties = [.. (merged.RequiredProperties ?? []).Concat(attr.RequiredProperties).Distinct()];
        }
    }

    /// <summary>
    /// Merges custom fields like XmlName.
    /// </summary>
    /// <param name="merged">The merged OpenApiPropertyAttribute to update.</param>
    /// <param name="attr">The OpenApiPropertyAttribute to merge from.</param>
    private static void MergeCustomFields(OpenApiPropertyAttribute merged, OpenApiPropertyAttribute attr)
    {
        if (!string.IsNullOrWhiteSpace(attr.XmlName))
        {
            merged.XmlName = attr.XmlName;
        }
    }
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
    private IOpenApiSchema InferPrimitiveSchema(Type type, bool inline = false)
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
            IOpenApiSchema? items = inline ? GetSchema(typeName).Clone() : new OpenApiSchemaReference(typeName);
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
        var schema = GetSchema(type.Name);

        if (inline)
        {
            if (schema is OpenApiSchema concreteSchema)
            {
                return concreteSchema.Clone();
            }
        }
        else
        {
            if (schema is not null)
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
        if (properties is not OpenApiParameterComponent)
        {
            if (properties.Description is not null)
            {
                schema.Description = properties.Description;
            }
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
        if (properties is not OpenApiParameterComponent)
        {
            schema.Deprecated = properties.Deprecated;
        }
    }

    private static void ApplyExamplesAndDefaults(OpenApiProperties properties, OpenApiSchema schema)
    {
        if (properties.Default is not null)
        {
            schema.Default = ToNode(properties.Default);
        }
        if (properties.Example is not null && properties is not OpenApiParameterComponent)
        {
            schema.Example = ToNode(properties.Example);
        }

        if (properties.Enum is { Length: > 0 })
        {
            schema.Enum = [.. properties.Enum.Select(ToNode).OfType<JsonNode>()];
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

    /// <summary>
    /// Determines if a type is considered primitive-like for schema generation.
    /// </summary>
    /// <param name="t">The type to check.</param>
    /// <returns>True if the type is considered primitive-like; otherwise, false.</returns>
    private static bool IsPrimitiveLike(Type t)
        => t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) ||
        t == typeof(Guid) || t == typeof(object) ||
        t == typeof(OpenApiString) || t == typeof(OpenApiInteger) ||
        t == typeof(OpenApiNumber) || t == typeof(OpenApiBoolean);

    #endregion

    /// <summary>
    /// Converts a .NET object to a JsonNode representation.
    /// </summary>
    /// <param name="value">The .NET object to convert.</param>
    /// <returns>A JsonNode representing the object, or null if the object is null.</returns>
    public static JsonNode? ToNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        // handle common types
        return value switch
        {
            bool b => JsonValue.Create(b),
            string s => JsonValue.Create(s),
            sbyte or byte or short or ushort or int or uint or long or ulong => JsonValue.Create(Convert.ToInt64(value)),
            float or double or decimal => JsonValue.Create(Convert.ToDouble(value)),
            DateTime dt => JsonValue.Create(dt.ToString("o")),
            Guid g => JsonValue.Create(g.ToString()),
            // Hashtable/IDictionary -> JsonObject
            System.Collections.IDictionary dict => ToJsonObject(dict),
            // Generic enumerable -> JsonArray
            IEnumerable<object?> seq => new JsonArray([.. seq.Select(ToNode)]),
            // Non-generic enumerable -> JsonArray
            System.Collections.IEnumerable en when value is not string => ToJsonArray(en),
            _ => ToNodeFromPocoOrString(value)
        };
    }

    private static JsonObject ToJsonObject(System.Collections.IDictionary dict)
    {
        var obj = new JsonObject();
        foreach (System.Collections.DictionaryEntry de in dict)
        {
            if (de.Key is null) { continue; }
            var k = de.Key.ToString() ?? string.Empty;
            obj[k] = ToNode(de.Value);
        }
        return obj;
    }

    private static JsonArray ToJsonArray(System.Collections.IEnumerable en)
    {
        var arr = new JsonArray();
        foreach (var item in en)
        {
            arr.Add(ToNode(item));
        }
        return arr;
    }

    private static JsonNode ToNodeFromPocoOrString(object value)
    {
        // Try POCO reflection
        var t = value.GetType();
        // Ignore types that are clearly not POCOs
        if (!t.IsPrimitive && t != typeof(string) && !typeof(System.Collections.IEnumerable).IsAssignableFrom(t))
        {
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (props.Length > 0)
            {
                var obj = new JsonObject();
                foreach (var p in props)
                {
                    if (!p.CanRead) { continue; }
                    var v = p.GetValue(value);
                    if (v is null) { continue; }
                    obj[p.Name] = ToNode(v);
                }
                return obj;
            }
        }
        // Fallback
        return JsonValue.Create(value?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// Ensures that a schema component exists for a complex .NET type.
    /// </summary>
    /// <param name="complexType">The complex .NET type.</param>
    private void EnsureSchemaComponent(Type complexType)
    {
        if (Document.Components?.Schemas != null && Document.Components.Schemas.ContainsKey(complexType.Name))
        {
            return;
        }

        var temp = new HashSet<Type>();
        BuildSchema(complexType, temp);
    }
}
