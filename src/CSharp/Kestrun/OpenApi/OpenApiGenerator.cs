// File: OpenApiV2Generator.cs
// Target: net8.0
// Requires: <PackageReference Include="Microsoft.OpenApi" Version="2.3.5" />
// NOTE: YAML reading needs Microsoft.OpenApi.YamlReader + settings.AddYamlReader(), but for generation you don't need it.

using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public static class OpenApiV2Generator
{
    /// <summary>
    /// Generates an OpenAPI document from the provided schema types.
    /// </summary>
    /// <param name="schemaTypes">The C# types to include in the OpenAPI document.</param>
    /// <param name="title">The title of the API.</param>
    /// <param name="version">The version of the API.</param>
    /// <returns>The generated OpenAPI document.</returns>
    public static OpenApiDocument Generate(IEnumerable<Type> schemaTypes, string title = "API", string version = "1.0.0")
    {
        var doc = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = title, Version = version },
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            },
            Paths = []
        };

        var built = new HashSet<Type>();
        foreach (var t in schemaTypes)
        {
            BuildSchema(t, doc, built);
        }

        return doc;
    }
    /// <summary>
    /// Serializes the OpenAPI document to a JSON string.
    /// </summary>
    /// <param name="doc">The OpenAPI document to serialize.</param>
    /// <param name="as31">Whether to serialize as OpenAPI 3.1.</param>
    /// <returns>The serialized JSON string.</returns>
    public static string ToJson(OpenApiDocument doc, bool as31 = true)
    {
        using var sw = new StringWriter();
        var w = new OpenApiJsonWriter(sw);
        if (as31)
        {
            doc.SerializeAsV31(w);
        }
        else
        {
            doc.SerializeAsV3(w);
        }

        return sw.ToString();
    }

    // ---- internals ----

    private static void BuildSchema(Type t, OpenApiDocument doc, HashSet<Type> built)
    {
        if (built.Contains(t))
        {
            return;
        }

        _ = built.Add(t);

        if (t.IsEnum)
        {
            if (doc.Components is not null && doc.Components.Schemas is not null)
            {
                doc.Components.Schemas[t.Name] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [.. t.GetEnumNames().Select(n => (JsonNode)n)]
                };
            }
            return;
        }

        if (IsPrimitiveLike(t))
        {
            return; // primitives don't go to components
        }

        var clsAttr = t.GetCustomAttribute<OpenApiSchemaAttribute>();
        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in t.GetCustomAttributes<OpenApiRequiredAttribute>())
        {
            _ = required.Add(r.Name);
        }

        var obj = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        };

        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var ps = BuildPropertySchema(p, doc, built);
            obj.Properties[p.Name] = ps;
            if (p.GetCustomAttribute<OpenApiRequiredPropertyAttribute>() != null)
            {
                _ = required.Add(p.Name);
            }
        }

        if (required.Count > 0)
        {
            obj.Required = required;
        }

        if (clsAttr != null)
        {
            ApplySchemaAttr(clsAttr, obj);
        }
        if (doc.Components is not null && doc.Components.Schemas is not null)
        {
            doc.Components.Schemas[t.Name] = obj;
        }
    }

    private static IOpenApiSchema BuildPropertySchema(PropertyInfo p, OpenApiDocument doc, HashSet<Type> built)
    {
        var pt = p.PropertyType;

        // complex type -> $ref via OpenApiSchemaReference
        if (!IsPrimitiveLike(pt) && !pt.IsEnum && !pt.IsArray)
        {
            BuildSchema(pt, doc, built); // ensure component exists
            var refSchema = new OpenApiSchemaReference(pt.Name);
            ApplySchemaAttr(p.GetCustomAttribute<OpenApiSchemaAttribute>(), refSchema);
            return refSchema;
        }

        // enum
        if (pt.IsEnum)
        {
            var s = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = [.. pt.GetEnumNames().Select(n => (JsonNode)n)]
            };
            ApplySchemaAttr(p.GetCustomAttribute<OpenApiSchemaAttribute>(), s);
            return s;
        }

        // array
        if (pt.IsArray)
        {
            var item = pt.GetElementType()!;
            IOpenApiSchema itemSchema;

            if (!IsPrimitiveLike(item) && !item.IsEnum)
            {
                // ensure the component exists
                BuildSchema(item, doc, built);
                // then reference it
                itemSchema = new OpenApiSchemaReference(item.Name);
            }
            else
            {
                itemSchema = InferPrimitiveSchema(item);
            }
            // then build the array schema
            var s = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = itemSchema
            };
            ApplySchemaAttr(p.GetCustomAttribute<OpenApiSchemaAttribute>(), s);
            return s;
        }

        // primitive
        var prim = InferPrimitiveSchema(pt);
        ApplySchemaAttr(p.GetCustomAttribute<OpenApiSchemaAttribute>(), prim);
        return prim;
    }

    private static OpenApiSchema InferPrimitiveSchema(Type t)
    {
        if (t == typeof(string))
        {
            return new() { Type = JsonSchemaType.String };
        }

        if (t == typeof(bool))
        {
            return new() { Type = JsonSchemaType.Boolean };
        }

        if (t == typeof(int) || t == typeof(short) || t == typeof(long) || t == typeof(byte))
        {
            return new() { Type = JsonSchemaType.Integer, Format = "int32" };
        }

        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
        {
            return new() { Type = JsonSchemaType.Number };
        }

        if (t == typeof(DateTime))
        {
            return new() { Type = JsonSchemaType.String, Format = "date-time" };
        }

        // default to string for other primitive-like types
        return t == typeof(Guid) ? new() { Type = JsonSchemaType.String, Format = "uuid" } :
        new() { Type = JsonSchemaType.String };
    }

    private static void ApplySchemaAttr(OpenApiSchemaAttribute? a, IOpenApiSchema s)
    {
        if (a == null)
        {
            return;
        }

        // Most models implement OpenApiSchema (concrete) OR OpenApiSchemaReference.
        // We set common metadata when possible (Description/Title apply only to concrete schema).
        if (s is OpenApiSchema sc)
        {
            if (!string.IsNullOrWhiteSpace(a.Title))
            {
                sc.Title = a.Title;
            }

            if (!string.IsNullOrWhiteSpace(a.Description))
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

            if (a.MaxLength.HasValue)
            {
                sc.MaxLength = a.MaxLength;
            }

            if (a.MinLength.HasValue)
            {
                sc.MinLength = a.MinLength;
            }

            if (!string.IsNullOrWhiteSpace(a.Pattern))
            {
                sc.Pattern = a.Pattern;
            }

            if (a.MaxItems.HasValue)
            {
                sc.MaxItems = a.MaxItems;
            }

            if (a.MinItems.HasValue)
            {
                sc.MinItems = a.MinItems;
            }

            if (a.UniqueItems)
            {
                sc.UniqueItems = true;
            }

            if (a.MaxProperties.HasValue)
            {
                sc.MaxProperties = a.MaxProperties;
            }

            if (a.MinProperties.HasValue)
            {
                sc.MinProperties = a.MinProperties;
            }

            sc.ReadOnly = a.ReadOnly;
            sc.WriteOnly = a.WriteOnly;
            sc.Deprecated = a.Deprecated;

            if (a.Default is not null)
            {
                sc.Default = ToNode(a.Default);
            }

            if (a.Example is not null)
            {
                sc.Example = ToNode(a.Example);
            }

            if (a is not null && a.Enum is not null && a.Enum is { Length: > 0 })
            {
                sc.Enum = [.. a.Enum.Select(ToNode).OfType<JsonNode>()];
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
        => t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(Guid);

    private static JsonNode? ToNode(object? value)
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
            IEnumerable<object?> seq => new JsonArray([.. seq.Select(ToNode)]),
            _ => JsonValue.Create(value.ToString())
        };
    }
}
