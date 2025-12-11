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
    private static OpenApiPropertyAttribute? GetSchemaIdentity(Type t)
    {
        // inherit:true already climbs the chain until it finds the first one
        var attrs = (OpenApiPropertyAttribute[])t.GetCustomAttributes(typeof(OpenApiPropertyAttribute), inherit: true);
        return attrs.Length > 0 ? attrs[0] : null;
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
        // Determine if the base type is a known OpenAPI primitive type
        OaSchemaType? baseTypeName = t.BaseType switch
        {
            Type bt when bt == typeof(OaString) => OaSchemaType.String,
            Type bt when bt == typeof(OaInteger) => OaSchemaType.Integer,
            Type bt when bt == typeof(OaNumber) => OaSchemaType.Number,
            Type bt when bt == typeof(OaBoolean) => OaSchemaType.Boolean,
            _ => null
        };

        if (typeof(IOpenApiType).IsAssignableFrom(t))
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
    private void CapturePropertyDefault(object? instance, PropertyInfo prop, IOpenApiSchema propSchema)
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

        if (attr.Required is { Length: > 0 })
        {
            merged.Required = [.. (merged.Required ?? []).Concat(attr.Required).Distinct()];
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

    /// <summary>
    /// Infers a primitive OpenApiSchema from a .NET type.
    /// </summary>
    /// <param name="type">The .NET type to infer from.</param>
    /// <param name="inline">Indicates if the schema should be inlined.</param>
    /// <returns>The inferred OpenApiSchema.</returns>
    private IOpenApiSchema InferPrimitiveSchema(Type type, bool inline = false)
    {
        // Direct type mappings
        if (PrimitiveSchemaMap.TryGetValue(type, out var schemaFactory))
        {
            return schemaFactory();
        }

        // Array type handling
        if (type.Name.EndsWith("[]"))
        {
            var typeName = type.Name[..^2];
            if (ComponentSchemasExists(typeName))
            {
                IOpenApiSchema? items = inline ? GetSchema(typeName).Clone() : new OpenApiSchemaReference(typeName);
                return new OpenApiSchema { Type = JsonSchemaType.Array, Items = items };
            }

            return new OpenApiSchema { Type = JsonSchemaType.Array, Items = InferPrimitiveSchema(type.GetElementType() ?? typeof(object)) };
        }

        // Special handling for PowerShell OpenAPI classes
        if (PowerShellOpenApiClassExporter.ValidClassNames.Contains(type.Name))
        {
            if (inline)
            {
                // Special case for PowerShell OpenAPI classes
                if (GetSchema(type.Name) is OpenApiSchema schema)
                {
                    return schema.Clone();
                }
                else
                {
                    Host.Logger.Warning("Schema for PowerShell OpenAPI class '{typeName}' not found. Defaulting to string schema.", type.Name);
                }
            }
            else
            {
                if (GetSchema(type.Name) is not null)
                {
                    return new OpenApiSchemaReference(type.Name);
                }
                else
                {
                    Host.Logger.Warning("Schema for PowerShell OpenAPI class '{typeName}' not found. Defaulting to string schema.", type.Name);
                }
            }
        }
        // Fallback
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
        [typeof(OaString)] = () => new OpenApiSchema { Type = JsonSchemaType.String },
        [typeof(OaInteger)] = () => new OpenApiSchema { Type = JsonSchemaType.Integer },
        [typeof(OaNumber)] = () => new OpenApiSchema { Type = JsonSchemaType.Number },
        [typeof(OaBoolean)] = () => new OpenApiSchema { Type = JsonSchemaType.Boolean }
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
        if (ioaSchema is OpenApiSchema sc)
        {
            if (oaProperties.Title is not null)
            {
                sc.Title = oaProperties.Title;
            }

            if (oaProperties.Description is not null)
            {
                sc.Description = oaProperties.Description;
            }

            if (oaProperties.Type != OaSchemaType.None)
            {
                sc.Type = oaProperties.Type switch
                {
                    OaSchemaType.String => JsonSchemaType.String,
                    OaSchemaType.Number => JsonSchemaType.Number,
                    OaSchemaType.Integer => JsonSchemaType.Integer,
                    OaSchemaType.Boolean => JsonSchemaType.Boolean,
                    OaSchemaType.Array => JsonSchemaType.Array,
                    OaSchemaType.Object => JsonSchemaType.Object,
                    OaSchemaType.Null => JsonSchemaType.Null,
                    _ => sc.Type
                };
            }
            if (oaProperties.Nullable)
            {
                sc.Type |= JsonSchemaType.Null;
            }

            if (!string.IsNullOrWhiteSpace(oaProperties.Format))
            {
                sc.Format = oaProperties.Format;
            }

            if (oaProperties.MultipleOf.HasValue)
            {
                sc.MultipleOf = oaProperties.MultipleOf;
            }

            if (!string.IsNullOrWhiteSpace(oaProperties.Maximum))
            {
                sc.Maximum = oaProperties.Maximum;
                if (oaProperties.ExclusiveMaximum)
                {
                    sc.ExclusiveMaximum = oaProperties.Maximum;
                }
            }
            if (!string.IsNullOrWhiteSpace(oaProperties.Minimum))
            {
                sc.Minimum = oaProperties.Minimum;
                if (oaProperties.ExclusiveMinimum)
                {
                    sc.ExclusiveMinimum = oaProperties.Minimum;
                }
            }

            if (oaProperties.MaxLength >= 0)
            {
                sc.MaxLength = oaProperties.MaxLength;
            }

            if (oaProperties.MinLength >= 0)
            {
                sc.MinLength = oaProperties.MinLength;
            }

            if (!string.IsNullOrWhiteSpace(oaProperties.Pattern))
            {
                sc.Pattern = oaProperties.Pattern;
            }

            if (oaProperties.MaxItems >= 0)
            {
                sc.MaxItems = oaProperties.MaxItems;
            }

            if (oaProperties.MinItems >= 0)
            {
                sc.MinItems = oaProperties.MinItems;
            }

            if (oaProperties.UniqueItems)
            {
                sc.UniqueItems = true;
            }

            if (oaProperties.MaxProperties >= 0)
            {
                sc.MaxProperties = oaProperties.MaxProperties;
            }

            if (oaProperties.MinProperties >= 0)
            {
                sc.MinProperties = oaProperties.MinProperties;
            }

            sc.ReadOnly = oaProperties.ReadOnly;
            sc.WriteOnly = oaProperties.WriteOnly;
            sc.Deprecated = oaProperties.Deprecated;
            // nullable bool

            sc.AdditionalPropertiesAllowed = oaProperties.AdditionalPropertiesAllowed;

            sc.UnevaluatedProperties = oaProperties.UnevaluatedProperties;
            if (oaProperties.Default is not null)
            {
                sc.Default = ToNode(oaProperties.Default);
            }

            if (oaProperties.Example is not null)
            {
                sc.Example = ToNode(oaProperties.Example);
            }

            if (oaProperties?.Enum is not null && oaProperties.Enum is { Length: > 0 })
            {
                sc.Enum = [.. oaProperties.Enum.Select(ToNode).OfType<JsonNode>()];
            }

            if (oaProperties?.Required is not null && oaProperties.Required.Length > 0)
            {
                sc.Required ??= new HashSet<string>(StringComparer.Ordinal);
                foreach (var r in oaProperties.Required)
                {
                    _ = sc.Required.Add(r);
                }
            }
        }
        else if (ioaSchema is OpenApiSchemaReference refSchema)
        {
            // Description/Title can live on a reference proxy in v2 (and serialize alongside $ref)
            if (!string.IsNullOrWhiteSpace(oaProperties.Description))
            {
                refSchema.Description = oaProperties.Description;
            }

            if (!string.IsNullOrWhiteSpace(oaProperties.Title))
            {
                refSchema.Title = oaProperties.Title;
            }

            // Example/Default/Enum aren’t typically set on the ref node itself;
            // attach such metadata to the component target instead if you need it.
        }
    }

    private static bool IsPrimitiveLike(Type t)
        => t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) ||
        t == typeof(Guid) || t == typeof(object) || t == typeof(OaString) || t == typeof(OaInteger) ||
         t == typeof(OaNumber) || t == typeof(OaBoolean);
    #endregion

    /// <summary>
    /// Converts a .NET object to a JsonNode representation.
    /// </summary>
    /// <param name="value">The .NET object to convert.</param>
    /// <returns>A JsonNode representing the object, or null if the object is null.</returns>
    internal static JsonNode? ToNode(object? value)
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
