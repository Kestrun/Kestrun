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

        if (t.BaseType is not null && t.BaseType != typeof(object))
        {
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
                var a = GetSchemaIdentity(t);
                if (a is not null)
                {
                    return new OpenApiSchema
                    {
                        Type = a.Type.ToJsonSchemaType(),
                        Format = a.Format
                    };
                }
            }
            else
            {
                IOpenApiSchema item = (baseTypeName is not null) ? new OpenApiSchema
                {
                    Type = baseTypeName?.ToJsonSchemaType()
                } : new OpenApiSchemaReference(t.BaseType.Name);
                var schemaComps = t.GetCustomAttributes<OpenApiProperties>()
                    .Where(schemaComp => schemaComp is not null)
                    .Cast<OpenApiProperties>();

                foreach (var prop in schemaComps)
                {
                    if (prop.Array)
                    {
                        var s = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Array,
                            Items = item
                        };
                        ApplySchemaAttr(prop, s);
                        return s;
                    }
                    else
                    {
                        var s = item; // Ensure base type schema is built first
                        ApplySchemaAttr(prop, s);
                        return s;
                    }
                }

                return item; // Ensure base type schema is built first
            }
        }
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        };
        if (built.Contains(t))
        {
            return schema;
        }

        _ = built.Add(t);

        if (t.IsEnum)
        {
            if (Document.Components is not null && Document.Components.Schemas is not null)
            {
                Document.Components.Schemas[t.Name] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [.. t.GetEnumNames().Select(n => (JsonNode)n)]
                };
            }
            return schema;
        }

        if (IsPrimitiveLike(t))
        {
            return schema; // primitives don't go to components
        }

        foreach (var a in t.GetCustomAttributes(true)
          .Where(a => a is OpenApiPropertyAttribute or OpenApiSchemaComponent))
        {
            ApplySchemaAttr(a as OpenApiProperties, schema);

            if (a is OpenApiSchemaComponent schemaAttribute)
            {
                if (schemaAttribute.Examples is not null)
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

        // Create an instance to capture default-initialized property values
        object? inst = null;
        try { inst = Activator.CreateInstance(t); } catch { inst = null; }

        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var ps = BuildPropertySchema(p, built);
            // If this is a concrete schema and no explicit default is set, try to use the property default value
            if (inst is not null && ps is OpenApiSchema concrete && concrete.Default is null)
            {
                try
                {
                    var val = p.GetValue(inst);
                    if (!IsIntrinsicDefault(val, p.PropertyType))
                    {
                        concrete.Default = ToNode(val);
                    }
                }
                catch { /* ignore */ }
            }
            if (p.GetCustomAttribute<OpenApiAdditionalPropertiesAttribute>() is not null)
            {
                schema.AdditionalPropertiesAllowed = true;
                schema.AdditionalProperties = ps;
            }
            else
            {
                schema.Properties[p.Name] = ps;
            }
        }
        return schema;
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
            // strings: last non-empty wins
            if (!string.IsNullOrWhiteSpace(a.Title))
            {
                m.Title = a.Title;
            }

            if (!string.IsNullOrWhiteSpace(a.Description))
            {
                m.Description = a.Description;
            }

            if (!string.IsNullOrWhiteSpace(a.Format))
            {
                m.Format = a.Format;
            }

            if (!string.IsNullOrWhiteSpace(a.Pattern))
            {
                m.Pattern = a.Pattern;
            }

            if (!string.IsNullOrWhiteSpace(a.Maximum))
            {
                m.Maximum = a.Maximum;
            }

            if (!string.IsNullOrWhiteSpace(a.Minimum))
            {
                m.Minimum = a.Minimum;
            }

            // enums/arrays: concatenate
            if (a.Enum is { Length: > 0 })
            {
                m.Enum = [.. m.Enum ?? [], .. a.Enum];
            }

            // defaults/examples: last non-null wins
            if (a.Default is not null)
            {
                m.Default = a.Default;
            }

            if (a.Example is not null)
            {
                m.Example = a.Example;
            }

            // numbers: prefer explicitly set (>=0 if you used sentinel; or HasValue if nullable)
            if (a.MaxLength >= 0)
            {
                m.MaxLength = a.MaxLength;
            }

            if (a.MinLength >= 0)
            {
                m.MinLength = a.MinLength;
            }

            if (a.MaxItems >= 0)
            {
                m.MaxItems = a.MaxItems;
            }

            if (a.MinItems >= 0)
            {
                m.MinItems = a.MinItems;
            }

            if (a.MultipleOf is not null)
            {
                m.MultipleOf = a.MultipleOf;
            }

            // booleans: OR them
            m.Nullable |= a.Nullable;
            m.ReadOnly |= a.ReadOnly;
            m.WriteOnly |= a.WriteOnly;
            m.Deprecated |= a.Deprecated;
            m.UniqueItems |= a.UniqueItems;
            m.ExclusiveMaximum |= a.ExclusiveMaximum;
            m.ExclusiveMinimum |= a.ExclusiveMinimum;

            // type override: last non-None wins
            if (a.Type != OaSchemaType.None)
            {
                m.Type = a.Type;
            }
            // array flag: OR them
            // collect Required names if your attribute exposes them
            if (a.Required is { Length: > 0 })
            {
                m.Required = [.. (m.Required ?? []).Concat(a.Required).Distinct()];
            }
            // carry any custom fields like XmlName if you have them
            if (!string.IsNullOrWhiteSpace(a.XmlName))
            {
                m.XmlName = a.XmlName;
            }
        }

        return m;
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


    private static void ApplySchemaAttr(OpenApiProperties? a, IOpenApiSchema s)
    {
        if (a is null)
        {
            return;
        }

        // Most models implement OpenApiSchema (concrete) OR OpenApiSchemaReference.
        // We set common metadata when possible (Description/Title apply only to concrete schema).
        if (s is OpenApiSchema sc)
        {
            if (a.Title is not null)
            {
                sc.Title = a.Title;
            }

            if (a.Description is not null)
            {
                sc.Description = a.Description;
            }

            if (a.Type != OaSchemaType.None)
            {
                sc.Type = a.Type switch
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
            if (a.Nullable)
            {
                sc.Type |= JsonSchemaType.Null;
            }

            if (!string.IsNullOrWhiteSpace(a.Format))
            {
                sc.Format = a.Format;
            }

            if (a.MultipleOf.HasValue)
            {
                sc.MultipleOf = a.MultipleOf;
            }

            if (!string.IsNullOrWhiteSpace(a.Maximum))
            {
                sc.Maximum = a.Maximum;
                if (a.ExclusiveMaximum)
                {
                    sc.ExclusiveMaximum = a.Maximum;
                }
            }
            if (!string.IsNullOrWhiteSpace(a.Minimum))
            {
                sc.Minimum = a.Minimum;
                if (a.ExclusiveMinimum)
                {
                    sc.ExclusiveMinimum = a.Minimum;
                }
            }

            if (a.MaxLength >= 0)
            {
                sc.MaxLength = a.MaxLength;
            }

            if (a.MinLength >= 0)
            {
                sc.MinLength = a.MinLength;
            }

            if (!string.IsNullOrWhiteSpace(a.Pattern))
            {
                sc.Pattern = a.Pattern;
            }

            if (a.MaxItems >= 0)
            {
                sc.MaxItems = a.MaxItems;
            }

            if (a.MinItems >= 0)
            {
                sc.MinItems = a.MinItems;
            }

            if (a.UniqueItems)
            {
                sc.UniqueItems = true;
            }

            if (a.MaxProperties >= 0)
            {
                sc.MaxProperties = a.MaxProperties;
            }

            if (a.MinProperties >= 0)
            {
                sc.MinProperties = a.MinProperties;
            }

            sc.ReadOnly = a.ReadOnly;
            sc.WriteOnly = a.WriteOnly;
            sc.Deprecated = a.Deprecated;
            // nullable bool

            sc.AdditionalPropertiesAllowed = a.AdditionalPropertiesAllowed;

            sc.UnevaluatedProperties = a.UnevaluatedProperties;
            if (a.Default is not null)
            {
                sc.Default = ToNode(a.Default);
            }

            if (a.Example is not null)
            {
                sc.Example = ToNode(a.Example);
            }

            if (a?.Enum is not null && a.Enum is { Length: > 0 })
            {
                sc.Enum = [.. a.Enum.Select(ToNode).OfType<JsonNode>()];
            }

            if (a?.Required is not null && a.Required.Length > 0)
            {
                sc.Required ??= new HashSet<string>(StringComparer.Ordinal);
                foreach (var r in a.Required)
                {
                    _ = sc.Required.Add(r);
                }
            }
        }
        else if (s is OpenApiSchemaReference refSchema)
        {
            // Description/Title can live on a reference proxy in v2 (and serialize alongside $ref)
            if (!string.IsNullOrWhiteSpace(a.Description))
            {
                refSchema.Description = a.Description;
            }

            if (!string.IsNullOrWhiteSpace(a.Title))
            {
                refSchema.Title = a.Title;
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
