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
    /// <param name="components">The set of discovered OpenAPI component types.</param>
    /// <param name="title">The title of the API.</param>
    /// <param name="version">The version of the API.</param>
    /// <returns>The generated OpenAPI document.</returns>
    public static OpenApiDocument Generate(OpenApiComponentSet components, string title = "API", string version = "1.0.0")
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
        foreach (var t in components.SchemaTypes)
        {
            BuildSchema(t, doc, built);
        }

        foreach (var t in components.ParameterTypes)
        {
            BuildParameters(t, doc);
        }

        foreach (var t in components.ResponseTypes)
        {
            BuildResponses(t, doc);
        }

        foreach (var t in components.ExampleTypes)
        {
            BuildExamples(t, doc);
        }

        foreach (var t in components.RequestBodyTypes)
        {
            BuildRequestBodies(t, doc);
        }

        foreach (var t in components.HeaderTypes)
        {
            BuildHeaders(t, doc);
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

        var clsAttrs = t.GetCustomAttributes<OpenApiSchemaAttribute>(inherit: false).ToArray();
        var clsAttr = MergeSchemaAttributes(clsAttrs);
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
    /// <summary>
    /// Generates an OpenAPI document from the provided schema types, with optional extra components.
    /// </summary>
    /// <param name="schemaTypes">The schema types to include in the document.</param>
    /// <param name="title">The title of the API.</param>
    /// <param name="version">The version of the API.</param>
    /// <param name="parameterTypes">Optional parameter types to include in the document.</param>
    /// <param name="extra">Optional extra components to include in the document.</param>
    /// <returns>The generated OpenAPI document.</returns>
    public static OpenApiDocument Generate(
              IEnumerable<Type> schemaTypes,

              string title = "API",
              string version = "1.0.0", IEnumerable<Type>? parameterTypes = null,
              OpenApiComponentsInput? extra = null)
    {
        var doc = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = title, Version = version },
            Components = new OpenApiComponents
            {
                // v2 wants IDictionary<string, IOpenApiSchema> for Schemas — these are filled below
                Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            },
            Paths = []
        };

        // build schemas from types (your existing logic)
        var built = new HashSet<Type>();
        foreach (var t in schemaTypes)
        {
            BuildSchema(t, doc, built);
        }

        if (parameterTypes != null)
        {
            foreach (var t in parameterTypes)
            {
                BuildParameters(t, doc);
            }
        }

        // merge optional component maps
        if (extra != null)
        {
            if (extra.Responses != null)
            {
                doc.Components.Responses = extra.Responses;
            }

            if (extra.Parameters != null)
            {
                doc.Components.Parameters = extra.Parameters;
            }

            if (extra.Examples != null)
            {
                doc.Components.Examples = extra.Examples;
            }

            if (extra.RequestBodies != null)
            {
                doc.Components.RequestBodies = extra.RequestBodies;
            }

            if (extra.Headers != null)
            {
                doc.Components.Headers = extra.Headers;
            }

            if (extra.SecuritySchemes != null)
            {
                doc.Components.SecuritySchemes = extra.SecuritySchemes;
            }

            if (extra.Links != null)
            {
                doc.Components.Links = extra.Links;
            }

            if (extra.Callbacks != null)
            {
                doc.Components.Callbacks = extra.Callbacks;
            }

            if (extra.PathItems != null)
            {
                doc.Components.PathItems = extra.PathItems;
            }

            if (extra.Extensions != null)
            {
                doc.Components.Extensions = extra.Extensions;
            }
        }

        return doc;
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
            var attrs = p.GetCustomAttributes<OpenApiSchemaAttribute>(inherit: false).ToArray();
            var a = MergeSchemaAttributes(attrs);
            ApplySchemaAttr(a, s);
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

    private static OpenApiSchemaAttribute? MergeSchemaAttributes(OpenApiSchemaAttribute[] attrs)
    {
        if (attrs == null || attrs.Length == 0)
        {
            return null;
        }

        if (attrs.Length == 1)
        {
            return attrs[0];
        }

        var m = new OpenApiSchemaAttribute();

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
            // Hashtable/IDictionary -> JsonObject
            System.Collections.IDictionary dict => ToJsonObject(dict),
            // Generic enumerable -> JsonArray
            IEnumerable<object?> seq => new JsonArray([.. seq.Select(ToNode)]),
            // Non-generic enumerable -> JsonArray
            System.Collections.IEnumerable en when value is not string => ToJsonArray(en),
            _ => JsonValue.Create(value.ToString())
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


    private static void BuildParameters(Type t, OpenApiDocument doc)
    {
        if (doc.Components!.Parameters == null)
        {
            doc.Components.Parameters = new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal);
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        foreach (var p in t.GetProperties(flags))
        {
            // Require [OpenApiParameter(...)] on the property
            var pAttr = p.GetCustomAttributes(inherit: false)
                         .FirstOrDefault(a => a.GetType().Name == "OpenApiParameterAttribute");
            if (pAttr is null)
            {
                continue;
            }

            // Reflect attribute fields (keeps this decoupled from your attribute assembly)
            var inVal = pAttr.GetType().GetProperty("In")?.GetValue(pAttr);
            var name = (string?)pAttr.GetType().GetProperty("Name")?.GetValue(pAttr) ?? p.Name;
            var required = (bool?)pAttr.GetType().GetProperty("Required")?.GetValue(pAttr) ?? false;
            var deprecated = (bool?)pAttr.GetType().GetProperty("Deprecated")?.GetValue(pAttr) ?? false;
            var allowEmptyVal = (bool?)pAttr.GetType().GetProperty("AllowEmptyValue")?.GetValue(pAttr) ?? false;
            var styleObj = pAttr.GetType().GetProperty("Style")?.GetValue(pAttr);   // OaParameterStyle?
            var _explode = (bool?)pAttr.GetType().GetProperty("Explode")?.GetValue(pAttr);
            var explode = (_explode is not null) && _explode.Value;
            var allowReserved = (bool?)pAttr.GetType().GetProperty("AllowReserved")?.GetValue(pAttr) ?? false;
            var exampleObj = pAttr.GetType().GetProperty("Example")?.GetValue(pAttr);

            // Merge any [OpenApiSchema(...)] attributes on the property (last one wins)
            var schemaAttr = p.GetCustomAttributes(inherit: false)
                              .Where(a => a.GetType().Name == "OpenApiSchemaAttribute")
                              .Cast<object>()
                              .LastOrDefault();

            IOpenApiSchema paramSchema;

            var pt = p.PropertyType;

            // ENUM → string + enum list
            if (pt.IsEnum)
            {
                var s = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [.. pt.GetEnumNames().Select(n => (JsonNode)n)]
                };
                ApplySchemaAttr(schemaAttr as OpenApiSchemaAttribute, s);
                paramSchema = s;
            }
            // ARRAY → array with item schema
            else if (pt.IsArray)
            {
                var elem = pt.GetElementType()!;
                IOpenApiSchema itemSchema;
                if (!IsPrimitiveLike(elem) && !elem.IsEnum)
                {
                    // ensure a component schema exists for the complex element and $ref it
                    EnsureSchemaComponent(elem, doc);
                    itemSchema = new OpenApiSchemaReference(elem.Name);
                }
                else if (elem.IsEnum)
                {
                    itemSchema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Enum = [.. elem.GetEnumNames().Select(n => (JsonNode)n)]
                    };
                }
                else
                {
                    itemSchema = InferPrimitiveSchema(elem);
                }

                var s = new OpenApiSchema { Type = JsonSchemaType.Array, Items = itemSchema };
                ApplySchemaAttr(schemaAttr as OpenApiSchemaAttribute, s);
                paramSchema = s;
            }
            // COMPLEX → ensure component + $ref
            else if (!IsPrimitiveLike(pt))
            {
                EnsureSchemaComponent(pt, doc);
                var r = new OpenApiSchemaReference(pt.Name);
                ApplySchemaAttr(schemaAttr as OpenApiSchemaAttribute, r);
                paramSchema = r;
            }
            // PRIMITIVE
            else
            {
                var s = InferPrimitiveSchema(pt);
                ApplySchemaAttr(schemaAttr as OpenApiSchemaAttribute, s);
                paramSchema = s;
            }

            // Build the OpenAPI parameter
            var param = new OpenApiParameter
            {
                Name = name,
                In = (inVal as OaParameterLocation? ?? OaParameterLocation.Query).ToOpenApi(),
                Required = required,
                Deprecated = deprecated,
                AllowEmptyValue = allowEmptyVal,
                Schema = paramSchema
            };

            // Optional hints
            if (styleObj is OaParameterStyle style)
            {
                param.Style = style.ToOpenApi();
            }

            if (explode)
            {
                param.Explode = explode;
            }

            param.AllowReserved = allowReserved;
            if (exampleObj is not null)
            {
                param.Example = ToNode(exampleObj);
            }

            // Compute component key using class-level JoinClassName (if present) or just member name
            var key = BuildParameterKey(t, p.Name);
            doc.Components.Parameters[key] = param;
        }

        // ---- local helpers ----

        // Ensure a schema component exists for a complex .NET type
        static void EnsureSchemaComponent(Type complexType, OpenApiDocument doc)
        {
            // If schema already present, bail
            if (doc.Components?.Schemas != null && doc.Components.Schemas.ContainsKey(complexType.Name))
            {
                return;
            }

            // Minimal one-off build (avoids needing the outer 'built' set)
            // If you prefer, you can expose and pass the outer HashSet<Type> instead.
            var temp = new HashSet<Type>();
            BuildSchema(complexType, doc, temp);
        }

        static string BuildParameterKey(Type declaringType, string memberName)
        {
            var mk = declaringType.GetCustomAttributes(inherit: false)
                                  .FirstOrDefault(a => a.GetType().Name == nameof(OpenApiModelKindAttribute));
            if (mk is null)
            {
                return memberName;
            }

            var join = mk.GetType().GetProperty("JoinClassName")?.GetValue(mk) as string;
            if (string.IsNullOrEmpty(join))
            {
                return memberName;
            }

            return $"{declaringType.Name}{join}{memberName}";
        }
    }

    private static void BuildResponses(Type t, OpenApiDocument doc)
    {
        if (doc.Components!.Responses == null)
        {
            doc.Components.Responses = new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal);
        }

        // 1) Class-level [OpenApiResponse(...)] attributes
        var classAttrs = t.GetCustomAttributes(inherit: false)
                          .Where(a => a.GetType().Name == nameof(OpenApiResponseAttribute))
                          .Cast<object>()
                          .ToArray();

        foreach (var a in classAttrs)
        {
            var resp = CreateResponseFromAttribute(a);
            var custom = GetNameOverride(a);
            var key = !string.IsNullOrWhiteSpace(custom) ? custom! : t.Name;
            doc.Components!.Responses![key] = resp;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        // 2) Property-level
        foreach (var p in t.GetProperties(flags))
        {
            var attrs = p.GetCustomAttributes(inherit: false)
                         .Where(a => a.GetType().Name == nameof(OpenApiResponseAttribute))
                         .Cast<object>()
                         .ToArray();

            if (attrs.Length == 0) { continue; }

            // Support multiple attributes per property
            foreach (var a in attrs)
            {
                var resp = CreateResponseFromAttribute(a);
                var custom = GetNameOverride(a);
                var key = !string.IsNullOrWhiteSpace(custom) ? custom! : BuildMemberResponseKey(t, p.Name);
                doc.Components!.Responses![key] = resp;
            }
        }

        // 3) Field-level (rare in PS classes, but supported by attribute)
        foreach (var f in t.GetFields(flags))
        {
            var attrs = f.GetCustomAttributes(inherit: false)
                         .Where(a => a.GetType().Name == nameof(OpenApiResponseAttribute))
                         .Cast<object>()
                         .ToArray();

            if (attrs.Length == 0) { continue; }

            foreach (var a in attrs)
            {
                var resp = CreateResponseFromAttribute(a);
                var custom = GetNameOverride(a);
                var key = !string.IsNullOrWhiteSpace(custom) ? custom! : BuildMemberResponseKey(t, f.Name);
                doc.Components!.Responses![key] = resp;
            }
        }
    }

    private static string? GetNameOverride(object attr)
    {
        var t = attr.GetType();
        return t.GetProperty("Name")?.GetValue(attr) as string;
    }

    private static string BuildMemberResponseKey(Type declaringType, string memberName)
    {
        // Look for [OpenApiModelKind(Kind) { JoinClassName = "-" }] on the declaring type
        var mk = declaringType.GetCustomAttributes(inherit: false)
                              .FirstOrDefault(a => a.GetType().Name == nameof(OpenApiModelKindAttribute));
        if (mk is null)
        {
            return memberName; // default: just member name
        }

        var join = mk.GetType().GetProperty("JoinClassName")?.GetValue(mk) as string;
        if (string.IsNullOrEmpty(join))
        {
            return memberName;
        }

        return $"{declaringType.Name}{join}{memberName}";
    }
    private static OpenApiResponse CreateResponseFromAttribute(object attr)
    {
        var t = attr.GetType();
        var description = t.GetProperty("Description")?.GetValue(attr) as string;
        var contentType = t.GetProperty("ContentType")?.GetValue(attr) as string ?? "application/json";
        var schemaRef = t.GetProperty("SchemaRef")?.GetValue(attr) as string;

        var response = new OpenApiResponse
        {
            Description = string.IsNullOrWhiteSpace(description) ? "Response" : description
        };

        if (!string.IsNullOrWhiteSpace(schemaRef))
        {
            response.Content = new Dictionary<string, OpenApiMediaType>
            {
                [contentType] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchemaReference(schemaRef)
                }
            };
        }

        // TODO: In future, map ExampleRef, HeaderRef, LinkRef when component builders exist.
        return response;
    }

    private static void BuildExamples(Type t, OpenApiDocument doc)
    {
        if (doc.Components!.Examples == null)
        {
            doc.Components.Examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        }

        // class-level
        var classAttrs = t.GetCustomAttributes(inherit: false)
                          .Where(a => a.GetType().Name == nameof(OpenApiExampleAttribute))
                          .ToArray();
        foreach (var a in classAttrs)
        {
            var ex = CreateExampleFromAttribute(a);
            var name = GetNameOverride(a) ?? t.Name;
            doc.Components!.Examples![name] = ex;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        // property-level
        foreach (var p in t.GetProperties(flags))
        {
            var attrs = p.GetCustomAttributes(inherit: false)
                         .Where(a => a.GetType().Name == nameof(OpenApiExampleAttribute))
                         .ToArray();
            foreach (var a in attrs)
            {
                var ex = CreateExampleFromAttribute(a);
                // If no inline or external value was provided, try to pull the default value from an instance
                if (ex.Value is null && string.IsNullOrWhiteSpace(ex.ExternalValue))
                {
                    try
                    {
                        var inst = Activator.CreateInstance(t);
                        var def = p.GetValue(inst);
                        if (def is not null)
                        {
                            ex.Value = ToNode(def);
                        }
                    }
                    catch { /* ignore */ }
                }
                var name = GetNameOverride(a) ?? BuildMemberResponseKey(t, p.Name); // reuse same join logic
                doc.Components!.Examples![name] = ex;
            }
        }

        // field-level
        foreach (var f in t.GetFields(flags))
        {
            var attrs = f.GetCustomAttributes(inherit: false)
                         .Where(a => a.GetType().Name == nameof(OpenApiExampleAttribute))
                         .ToArray();
            foreach (var a in attrs)
            {
                var ex = CreateExampleFromAttribute(a);
                if (ex.Value is null && string.IsNullOrWhiteSpace(ex.ExternalValue))
                {
                    try
                    {
                        var inst = Activator.CreateInstance(t);
                        var def = f.GetValue(inst);
                        if (def is not null)
                        {
                            ex.Value = ToNode(def);
                        }
                    }
                    catch { /* ignore */ }
                }
                var name = GetNameOverride(a) ?? BuildMemberResponseKey(t, f.Name);
                doc.Components!.Examples![name] = ex;
            }
        }

        static OpenApiExample CreateExampleFromAttribute(object attr)
        {
            var t = attr.GetType();
            var summary = t.GetProperty("Summary")?.GetValue(attr) as string;
            var description = t.GetProperty("Description")?.GetValue(attr) as string;
            var value = t.GetProperty("Value")?.GetValue(attr);
            var external = t.GetProperty("ExternalValue")?.GetValue(attr) as string;

            var ex = new OpenApiExample
            {
                Summary = summary,
                Description = description
            };

            if (value is not null)
            {
                ex.Value = ToNode(value);
            }

            if (!string.IsNullOrWhiteSpace(external))
            {
                ex.ExternalValue = external;
            }

            return ex;
        }
    }

    private static void BuildRequestBodies(Type t, OpenApiDocument doc)
    {
        doc.Components!.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal);

        // class-level
        var classAttrs = t.GetCustomAttributes(inherit: false)
                          .Where(a => a.GetType().Name == nameof(OpenApiRequestBodyAttribute))
                          .ToArray();
        foreach (var a in classAttrs)
        {
            var rb = CreateRequestBodyFromAttribute(a);
            var name = GetNameOverride(a) ?? t.Name;
            doc.Components!.RequestBodies![name] = rb;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        // property-level
        foreach (var p in t.GetProperties(flags))
        {
            var attrs = p.GetCustomAttributes(inherit: false)
                         .Where(a => a.GetType().Name == nameof(OpenApiRequestBodyAttribute))
                         .ToArray();
            foreach (var a in attrs)
            {
                var rb = CreateRequestBodyFromAttribute(a);
                // synthesize example from default if none provided
                if ((rb.Content?.Count ?? 0) > 0)
                {
                    TryPopulateExampleFromDefault(t, p, rb);
                }
                var name = GetNameOverride(a) ?? BuildMemberResponseKey(t, p.Name);
                doc.Components!.RequestBodies![name] = rb;
            }
        }

        // field-level
        foreach (var f in t.GetFields(flags))
        {
            var attrs = f.GetCustomAttributes(inherit: false)
                         .Where(a => a.GetType().Name == nameof(OpenApiRequestBodyAttribute))
                         .ToArray();
            foreach (var a in attrs)
            {
                var rb = CreateRequestBodyFromAttribute(a);
                if ((rb.Content?.Count ?? 0) > 0)
                {
                    TryPopulateExampleFromDefault(t, f, rb);
                }
                var name = GetNameOverride(a) ?? BuildMemberResponseKey(t, f.Name);
                doc.Components!.RequestBodies![name] = rb;
            }
        }

        static void TryPopulateExampleFromDefault(Type t, MemberInfo m, OpenApiRequestBody rb)
        {
            // Only add example if none present
            if (rb.Content is null || rb.Content.Count == 0) { return; }
            foreach (var mt in rb.Content.Values)
            {
                if (mt.Example != null) { return; }
            }
            try
            {
                var inst = Activator.CreateInstance(t);
                var def = m switch
                {
                    PropertyInfo pi => pi.GetValue(inst),
                    FieldInfo fi => fi.GetValue(inst),
                    _ => null
                };
                if (def is not null)
                {
                    // Use first content type entry
                    var first = rb.Content.First().Value;
                    first.Example = ToNode(def);
                }
            }
            catch { }
        }
    }

    private static OpenApiRequestBody CreateRequestBodyFromAttribute(object attr)
    {
        var t = attr.GetType();
        var description = t.GetProperty("Description")?.GetValue(attr) as string;
        var contentType = t.GetProperty("ContentType")?.GetValue(attr) as string ?? "application/json";
        var schemaRef = t.GetProperty("SchemaRef")?.GetValue(attr) as string;
        var required = (bool?)t.GetProperty("Required")?.GetValue(attr) ?? false;
        var example = t.GetProperty("Example")?.GetValue(attr);

        var rb = new OpenApiRequestBody
        {
            Description = description,
            Required = required,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [contentType] = new OpenApiMediaType
                {
                    Schema = string.IsNullOrWhiteSpace(schemaRef) ? null : new OpenApiSchemaReference(schemaRef)
                }
            }
        };

        if (example is not null)
        {
            rb.Content![contentType].Example = ToNode(example);
        }

        return rb;
    }

    private static void BuildHeaders(Type t, OpenApiDocument doc)
    {
        doc.Components!.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);

        // class-level
        var classAttrs = t.GetCustomAttributes(inherit: false)
                          .Where(a => a.GetType().Name == nameof(OpenApiHeaderAttribute))
                          .ToArray();
        foreach (var a in classAttrs)
        {
            var h = CreateHeaderFromAttribute(a);
            var name = GetNameOverride(a) ?? t.Name;
            doc.Components!.Headers![name] = h;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        // property-level
        foreach (var p in t.GetProperties(flags))
        {
            var attrs = p.GetCustomAttributes(inherit: false)
                         .Where(a => a.GetType().Name == nameof(OpenApiHeaderAttribute))
                         .ToArray();
            foreach (var a in attrs)
            {
                var h = CreateHeaderFromAttribute(a);
                var name = GetNameOverride(a) ?? BuildMemberResponseKey(t, p.Name);
                doc.Components!.Headers![name] = h;
            }
        }

        // field-level
        foreach (var f in t.GetFields(flags))
        {
            var attrs = f.GetCustomAttributes(inherit: false)
                         .Where(a => a.GetType().Name == nameof(OpenApiHeaderAttribute))
                         .ToArray();
            foreach (var a in attrs)
            {
                var h = CreateHeaderFromAttribute(a);
                var name = GetNameOverride(a) ?? BuildMemberResponseKey(t, f.Name);
                doc.Components!.Headers![name] = h;
            }
        }
    }

    private static OpenApiHeader CreateHeaderFromAttribute(object attr)
    {
        var t = attr.GetType();
        var description = t.GetProperty("Description")?.GetValue(attr) as string;
        var required = (bool?)t.GetProperty("Required")?.GetValue(attr) ?? false;
        var deprecated = (bool?)t.GetProperty("Deprecated")?.GetValue(attr) ?? false;
        var allowEmptyVal = (bool?)t.GetProperty("AllowEmptyValue")?.GetValue(attr) ?? false;
        var styleObj = t.GetProperty("Style")?.GetValue(attr);
        var explodeObj = t.GetProperty("Explode")?.GetValue(attr);
        var explode = (explodeObj is bool b) && b;
        var schemaRef = t.GetProperty("SchemaRef")?.GetValue(attr) as string;
        var example = t.GetProperty("Example")?.GetValue(attr);

        var header = new OpenApiHeader
        {
            Description = description,
            Required = required,
            Deprecated = deprecated,
            AllowEmptyValue = allowEmptyVal,
            Schema = string.IsNullOrWhiteSpace(schemaRef) ? new OpenApiSchema { Type = JsonSchemaType.String } : new OpenApiSchemaReference(schemaRef)
        };

        if (styleObj is OaParameterStyle style)
        {
            header.Style = style.ToOpenApi();
        }
        if (explode) { header.Explode = true; }
        if (example is not null) { header.Example = ToNode(example); }

        return header;
    }
}
