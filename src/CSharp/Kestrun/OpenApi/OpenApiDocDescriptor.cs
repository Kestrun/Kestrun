// File: OpenApiV2Generator.cs
// Target: net8.0
// Requires: <PackageReference Include="Microsoft.OpenApi" Version="2.3.5" />
// NOTE: YAML reading needs Microsoft.OpenApi.YamlReader + settings.AddYamlReader(), but for generation you don't need it.

using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Utilities;
namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
/// <param name="host">The Kestrun host providing registered routes.</param>
/// <param name="docId">The ID of the OpenAPI document being generated.</param>
public class OpenApiDocDescriptor(KestrunHost host, string docId)
{
    /// <summary>
    /// The Kestrun host providing registered routes.
    /// </summary>
    public KestrunHost Host { get; init; } = host;

    /// <summary>
    /// The ID of the OpenAPI document being generated.
    /// </summary>
    public string DocumentId { get; init; } = docId;

    /// <summary>
    /// The OpenAPI document being generated.
    /// </summary>
    public OpenApiDocument Document { get; init; } = new OpenApiDocument { Components = new OpenApiComponents() };


    /// <summary>
    /// Generates an OpenAPI document from the provided schema types.
    /// </summary>
    /// <param name="components">The set of discovered OpenAPI component types.</param>
    /// <returns>The generated OpenAPI document.</returns>
    public void Generate(OpenApiComponentSet components)
    {
        Document.Paths = [];

        BuildPathsFromRegisteredRoutes(Host.RegisteredRoutes);

        if (Document.Components!.Schemas == null)
        {

            Document.Components!.Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        }

        foreach (var t in components.SchemaTypes)
        {
            BuildSchema(t);
        }


        foreach (var t in components.ParameterTypes)
        {
            BuildParameters(t);
        }

        foreach (var t in components.ResponseTypes)
        {
            BuildResponses(t);
        }

        foreach (var t in components.ExampleTypes)
        {
            BuildExamples(t);
        }

        foreach (var t in components.RequestBodyTypes)
        {
            BuildRequestBodies(t);
        }

        foreach (var t in components.HeaderTypes)
        {
            BuildHeaders(t);
        }

        // Links
        foreach (var t in components.LinkTypes)
        {
            BuildLinks(t);
        }

        // Callbacks
        foreach (var t in components.CallbackTypes)
        {
            BuildCallbacks(t);
        }
    }

    /// <summary>
    /// Serializes the OpenAPI document to a JSON string.
    /// </summary>
    /// <param name="version">The OpenAPI specification version to serialize as.</param>
    /// <returns>The serialized JSON string.</returns>
    public string ToJson(OpenApiSpecVersion version)
    {
        using var sw = new StringWriter();
        var w = new OpenApiJsonWriter(sw);
        Document.SerializeAs(version, w);
        return sw.ToString();
    }

    /// <summary>
    /// Serializes the OpenAPI document to a YAML string.
    /// </summary>
    /// <param name="version">The OpenAPI specification version to serialize as.</param>
    /// <returns>The serialized YAML string.</returns>
    public string ToYaml(OpenApiSpecVersion version)
    {
        using var sw = new StringWriter();
        var w = new OpenApiYamlWriter(sw);
        Document.SerializeAs(version, w);
        return sw.ToString();
    }


    // ---- internals ----

    /// <summary>
    /// Populates Document.Paths from the registered routes using OpenAPI metadata on each route.
    /// </summary>
    private void BuildPathsFromRegisteredRoutes(IDictionary<(string Pattern, HttpVerb Method), MapRouteOptions> routes)
    {
        if (routes is null || routes.Count == 0)
        {
            return;
        }

        // Group by path pattern
        foreach (var grp in routes
            .GroupBy(kvp => kvp.Key.Pattern, StringComparer.Ordinal)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .Where(g => g.Any(kvp => kvp.Value?.OpenAPI?.Count > 0)))
        {
            OpenAPICommonMetadata? pathMeta = null;
            var pattern = grp.Key;

            // Ensure a PathItem exists
            Document.Paths ??= [];

            if (!Document.Paths.TryGetValue(pattern, out var pathInterface) || pathInterface is null)
            {
                pathInterface = new OpenApiPathItem();
                Document.Paths[pattern] = pathInterface;
            }

            var pathItem = (OpenApiPathItem)pathInterface;
            var multipleMethods = grp.Count() > 1;
            foreach (var kvp in grp)
            {
                var method = kvp.Key.Method;
                var map = kvp.Value;
                if (map is null || map.OpenAPI.Count == 0)
                {
                    continue;
                }

                if ((map.PathLevelOpenAPIMetadata is not null) && (pathMeta is null))
                {
                    pathMeta = map.PathLevelOpenAPIMetadata;
                }

                // Decide whether to include the operation. Prefer explicit enable, but also include when metadata is present.
                var meta = map.OpenAPI[method];
                if (meta is null || !meta.Enabled)
                {
                    // Skip silent routes by default
                    continue;
                }
                /*   if (multipleMethods)
                   {
                       pathItem.Description = meta.Description;
                       pathItem.Summary = meta.Summary;
                   }*/
                var op = BuildOperationFromMetadata(meta);

                pathItem.AddOperation(HttpMethod.Parse(method.ToMethodString()), op);
            }
            // Optionally apply servers/parameters at the path level for quick discovery in PS views
            if (pathMeta is not null)
            {
                pathItem.Description = pathMeta.Description;
                pathItem.Summary = pathMeta.Summary;
                try
                {
                    if (pathMeta.Servers is { Count: > 0 })
                    {
                        dynamic dPath = pathItem!;
                        if (dPath.Servers == null) { dPath.Servers = new List<OpenApiServer>(); }
                        foreach (var s in pathMeta.Servers)
                        {
                            dPath.Servers.Add(s);
                        }
                    }
                    if (pathMeta.Parameters is { Count: > 0 })
                    {
                        dynamic dPath = pathItem!;
                        if (dPath.Parameters == null) { dPath.Parameters = new List<IOpenApiParameter>(); }
                        foreach (var p in pathMeta.Parameters)
                        {
                            dPath.Parameters.Add(p);
                        }
                    }
                }
                catch { /* tolerate differing model shapes */ }
            }

        }
    }

    /// <summary>
    /// Builds an OpenApiOperation from OpenAPIMetadata.
    /// </summary>
    /// <param name="meta">The OpenAPIMetadata to build from.</param>
    /// <returns>The constructed OpenApiOperation.</returns>
    private static OpenApiOperation BuildOperationFromMetadata(OpenAPIMetadata meta)
    {
        var op = new OpenApiOperation
        {
            OperationId = string.IsNullOrWhiteSpace(meta.OperationId) ? null : meta.OperationId,

            Summary = string.IsNullOrWhiteSpace(meta.Summary) ? null : meta.Summary,
            Description = string.IsNullOrWhiteSpace(meta.Description) ? null : meta.Description,
            Deprecated = meta.Deprecated
        };
        // Tags
        if (meta.Tags.Length > 0)
        {
            op.Tags = new HashSet<OpenApiTagReference>();
            foreach (var t in meta.Tags ?? [])
            {
                _ = op.Tags.Add(new OpenApiTagReference(t));
            }
        }
        // External docs
        if (meta.ExternalDocs is not null)
        {
            op.ExternalDocs = meta.ExternalDocs;
        }

        // Servers (operation-level)
        try
        {
            if (meta.Servers is { Count: > 0 })
            {
                dynamic d = op;
                if (d.Servers == null) { d.Servers = new List<OpenApiServer>(); }
                foreach (var s in meta.Servers) { d.Servers.Add(s); }
            }
        }
        catch { }

        // Parameters (operation-level)
        try
        {
            if (meta.Parameters is { Count: > 0 })
            {
                dynamic d = op;
                if (d.Parameters == null) { d.Parameters = new List<IOpenApiParameter>(); }
                foreach (var p in meta.Parameters) { d.Parameters.Add(p); }
            }
        }
        catch { }


        // Request body
        if (meta.RequestBody is not null)
        {
            op.RequestBody = meta.RequestBody;
        }

        // Responses (required by spec)
        op.Responses = meta.Responses ?? new OpenApiResponses { ["200"] = new OpenApiResponse { Description = "Success" } };

        // Callbacks
        if (meta.Callbacks is not null && meta.Callbacks.Count > 0)
        {
            op.Callbacks = new Dictionary<string, IOpenApiCallback>(meta.Callbacks);
        }

        return op;
    }


    private void BuildSchema(Type t, HashSet<Type>? built = null)
    {
        built ??= [];
        if (built.Contains(t))
        {
            return;
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
                    var def = p.GetValue(inst);
                    if (def is not null)
                    {
                        concrete.Default = ToNode(def);
                    }
                }
                catch { /* ignore */ }
            }
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
        if (Document.Components is not null && Document.Components.Schemas is not null)
        {
            Document.Components.Schemas[t.Name] = obj;
        }
    }



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

        // complex type -> $ref via OpenApiSchemaReference
        if (!IsPrimitiveLike(pt) && !pt.IsEnum && !pt.IsArray)
        {
            BuildSchema(pt, built); // ensure component exists
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
            ApplyPowerShellValidationAttributes(p, s);
            if (allowNull)
            {
                s.Type |= JsonSchemaType.Null;
            }
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
                BuildSchema(item, built);
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
            ApplyPowerShellValidationAttributes(p, s);
            if (allowNull)
            {
                s.Type |= JsonSchemaType.Null;
            }
            return s;
        }

        // primitive
        var prim = InferPrimitiveSchema(pt);
        ApplySchemaAttr(p.GetCustomAttribute<OpenApiSchemaAttribute>(), prim);
        ApplyPowerShellValidationAttributes(p, prim);
        if (allowNull)
        {
            prim.Type |= JsonSchemaType.Null;
        }
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

        // Integer types
        if (t == typeof(int) || t == typeof(short) || t == typeof(byte))
        {
            return new() { Type = JsonSchemaType.Integer, Format = "int32" };
        }
        if (t == typeof(long))
        {
            return new() { Type = JsonSchemaType.Integer, Format = "int64" };
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
        return JsonValue.Create(value?.ToString() ?? string.Empty)!;
    }

    // Ensure a schema component exists for a complex .NET type
    private void EnsureSchemaComponent(Type complexType)
    {
        if (Document.Components?.Schemas != null && Document.Components.Schemas.ContainsKey(complexType.Name))
        {
            return;
        }

        var temp = new HashSet<Type>();
        BuildSchema(complexType, temp);
    }


    private void BuildParameters(Type t)
    {
        if (Document.Components!.Parameters == null)
        {
            Document.Components.Parameters = new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal);
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        // Enforce class-first parameter convention: a [OpenApiModelKind(Parameters)] class
        // should declare exactly one property decorated with [OpenApiParameter].
        var isParamKindClass = t.GetCustomAttributes(inherit: false)
                                .Any(a => a.GetType().Name == nameof(OpenApiModelKindAttribute)
                                       && string.Equals(
                                           a.GetType().GetProperty("Kind")?.GetValue(a)?.ToString(),
                                           nameof(OpenApiModelKind.Parameters), StringComparison.Ordinal));
        if (isParamKindClass)
        {
            var paramPropCount = t.GetProperties(flags)
                                   .Count(p => p.GetCustomAttributes(inherit: false)
                                                 .Any(a => a.GetType().Name == nameof(OpenApiParameterAttribute)));
            if (paramPropCount > 1)
            {
                throw new InvalidOperationException($"OpenAPI Parameters class '{t.FullName}' must define exactly one property decorated with [OpenApiParameter]. Found {paramPropCount}.");
            }
        }

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
            var allowNull = false;
            var underlying = Nullable.GetUnderlyingType(pt);
            if (underlying != null)
            {
                allowNull = true;
                pt = underlying;
            }

            // ENUM → string + enum list
            if (pt.IsEnum)
            {
                var s = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [.. pt.GetEnumNames().Select(n => (JsonNode)n)]
                };
                ApplySchemaAttr(schemaAttr as OpenApiSchemaAttribute, s);
                if (allowNull)
                {
                    s.Type |= JsonSchemaType.Null;
                }
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
                    EnsureSchemaComponent(elem);
                    itemSchema = new OpenApiSchemaReference(elem.Name);
                }
                else
                {
                    itemSchema = elem.IsEnum
                        ? new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Enum = [.. elem.GetEnumNames().Select(n => (JsonNode)n)]
                        }
                        : InferPrimitiveSchema(elem);
                }

                var s = new OpenApiSchema { Type = JsonSchemaType.Array, Items = itemSchema };
                ApplySchemaAttr(schemaAttr as OpenApiSchemaAttribute, s);
                ApplyPowerShellValidationAttributes(p, s);
                if (allowNull)
                {
                    s.Type |= JsonSchemaType.Null;
                }
                paramSchema = s;
            }
            // COMPLEX → ensure component + $ref
            else if (!IsPrimitiveLike(pt))
            {
                EnsureSchemaComponent(pt);
                var r = new OpenApiSchemaReference(pt.Name);
                ApplySchemaAttr(schemaAttr as OpenApiSchemaAttribute, r);
                paramSchema = r;
            }
            // PRIMITIVE
            else
            {
                var s = InferPrimitiveSchema(pt);
                ApplySchemaAttr(schemaAttr as OpenApiSchemaAttribute, s);
                ApplyPowerShellValidationAttributes(p, s);
                // If no explicit default provided via schema attribute, try to pull default from property value
                if (s is OpenApiSchema sc && sc.Default is null)
                {
                    try
                    {
                        var inst = Activator.CreateInstance(t);
                        var def = p.GetValue(inst);
                        if (def is not null)
                        {
                            sc.Default = ToNode(def);
                        }
                    }
                    catch { /* ignore */ }
                }
                if (allowNull)
                {
                    s.Type |= JsonSchemaType.Null;
                }
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
            Document.Components.Parameters[key] = param;
        }

        // ---- local helpers ----


        static string BuildParameterKey(Type declaringType, string memberName)
        {
            var mk = declaringType.GetCustomAttributes(inherit: false)
                                  .FirstOrDefault(a => a.GetType().Name == nameof(OpenApiModelKindAttribute));
            if (mk is null)
            {
                return memberName;
            }

            var join = mk.GetType().GetProperty("JoinClassName")?.GetValue(mk) as string;
            return string.IsNullOrEmpty(join) ? memberName : $"{declaringType.Name}{join}{memberName}";
        }
    }

    private void BuildResponses(Type t)
    {
        if (Document.Components!.Responses == null)
        {
            Document.Components.Responses = new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal);
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
            Document.Components!.Responses![key] = resp;
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
                Document.Components!.Responses![key] = resp;
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
                Document.Components!.Responses![key] = resp;
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
        return string.IsNullOrEmpty(join) ? memberName : $"{declaringType.Name}{join}{memberName}";
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

    private void BuildExamples(Type t)
    {
        if (Document.Components!.Examples == null)
        {
            Document.Components.Examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        }

        // class-level
        var classAttrs = t.GetCustomAttributes(inherit: false)
                          .Where(a => a.GetType().Name == nameof(OpenApiExampleAttribute))
                          .ToArray();
        foreach (var a in classAttrs)
        {
            var ex = CreateExampleFromAttribute(a);
            var name = GetNameOverride(a) ?? t.Name;
            Document.Components!.Examples![name] = ex;
        }

        // If no class-level [OpenApiExample] attribute but the class is marked as Example kind,
        // create an example from the class instance defaults
        if (classAttrs.Length == 0)
        {
            var mk = t.GetCustomAttributes(inherit: false)
                      .FirstOrDefault(a => a.GetType().Name == nameof(OpenApiModelKindAttribute));
            if (mk is not null)
            {
                var kindVal = mk.GetType().GetProperty("Kind")?.GetValue(mk)?.ToString();
                if (string.Equals(kindVal, nameof(OpenApiModelKind.Example), StringComparison.Ordinal))
                {
                    try
                    {
                        var inst = Activator.CreateInstance(t);
                        var ex = new OpenApiExample { Value = ToNode(inst) };
                        Document.Components!.Examples![t.Name] = ex;
                    }
                    catch { /* ignore */ }
                }
            }
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
                Document.Components!.Examples![name] = ex;
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
                Document.Components!.Examples![name] = ex;
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

    private void BuildRequestBodies(Type t)
    {
        Document.Components!.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal);

        // class-level
        var classAttrs = t.GetCustomAttributes(inherit: false)
                          .Where(a => a.GetType().Name == nameof(OpenApiRequestBodyAttribute))
                          .ToArray();
        foreach (var a in classAttrs)
        {
            var rb = CreateRequestBodyFromAttribute(a, t);
            var name = GetNameOverride(a) ?? t.Name;
            Document.Components!.RequestBodies![name] = rb;
        }

        // If no class-level [OpenApiRequestBody] but the class is marked RequestBody kind,
        // generate a request body that references this class schema and uses instance defaults as example
        if (classAttrs.Length == 0)
        {
            var mk = t.GetCustomAttributes(inherit: false)
                      .FirstOrDefault(a => a.GetType().Name == nameof(OpenApiModelKindAttribute));
            if (mk is not null)
            {
                var kindVal = mk.GetType().GetProperty("Kind")?.GetValue(mk)?.ToString();
                if (string.Equals(kindVal, nameof(OpenApiModelKind.RequestBody), StringComparison.Ordinal))
                {
                    var inline = (bool?)mk.GetType().GetProperty("InlineSchema")?.GetValue(mk) ?? false;
                    if (!inline)
                    {
                        // Ensure a schema exists for this class and reference it
                        EnsureSchemaComponent(t);
                    }
                    var rb = new OpenApiRequestBody
                    {
                        Description = "Request body",
                        Required = false,
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Schema = inline ? BuildInlineSchemaFromType(t) : new OpenApiSchemaReference(t.Name)
                            }
                        }
                    };
                    try
                    {
                        var inst = Activator.CreateInstance(t);
                        rb.Content["application/json"].Example = ToNode(inst);
                    }
                    catch { /* ignore */ }

                    Document.Components!.RequestBodies![t.Name] = rb;
                }
            }
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
                Document.Components!.RequestBodies![name] = rb;
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
                Document.Components!.RequestBodies![name] = rb;
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

    private OpenApiRequestBody CreateRequestBodyFromAttribute(object attr, Type? declaringType = null)
    {
        var t = attr.GetType();
        var description = t.GetProperty("Description")?.GetValue(attr) as string;
        var contentType = t.GetProperty("ContentType")?.GetValue(attr) as string ?? "application/json";
        var schemaRef = t.GetProperty("SchemaRef")?.GetValue(attr) as string;
        var required = (bool?)t.GetProperty("Required")?.GetValue(attr) ?? false;
        var example = t.GetProperty("Example")?.GetValue(attr);
        var inline = (bool?)t.GetProperty("Inline")?.GetValue(attr) ?? false;

        var rb = new OpenApiRequestBody
        {
            Description = description,
            Required = required,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [contentType] = new OpenApiMediaType
                {
                    Schema = inline
                        ? (!string.IsNullOrWhiteSpace(schemaRef)
                            ? BuildInlineSchemaFromRefOrType(schemaRef, declaringType)
                            : (declaringType != null ? BuildInlineSchemaFromType(declaringType) : null))
                        : (string.IsNullOrWhiteSpace(schemaRef) ? null : new OpenApiSchemaReference(schemaRef))
                }
            }
        };

        if (example is not null)
        {
            rb.Content![contentType].Example = ToNode(example);
        }

        return rb;
    }

    private IOpenApiSchema? BuildInlineSchemaFromRefOrType(string _, Type? fallbackType)
        => fallbackType != null ? BuildInlineSchemaFromType(fallbackType) : new OpenApiSchema { Type = JsonSchemaType.Object };



    // Overload that ensures nested complex types have component schemas available in the document
    private IOpenApiSchema BuildInlineSchemaFromType(Type t)
    {
        var obj = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        };

        // Instantiate to capture default-initialized values from the class for property-level defaults
        object? inst = null;
        try { inst = Activator.CreateInstance(t); } catch { inst = null; }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var p in t.GetProperties(flags))
        {
            var pt = p.PropertyType;
            var allowNull = false;
            var underlying = Nullable.GetUnderlyingType(pt);
            if (underlying != null)
            {
                allowNull = true;
                pt = underlying;
            }
            IOpenApiSchema ps;
            if (pt.IsEnum)
            {
                ps = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [.. pt.GetEnumNames().Select(n => (JsonNode)n)] };
            }
            else if (pt.IsArray)
            {
                var elem = pt.GetElementType()!;
                IOpenApiSchema item;
                if (elem.IsEnum)
                {
                    item = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [.. elem.GetEnumNames().Select(n => (JsonNode)n)] };
                }
                else if (IsPrimitiveLike(elem))
                {
                    item = InferPrimitiveSchema(elem);
                }
                else
                {
                    EnsureSchemaComponent(elem);
                    item = new OpenApiSchemaReference(elem.Name);
                }
                ps = new OpenApiSchema { Type = JsonSchemaType.Array, Items = item };
            }
            else if (!IsPrimitiveLike(pt))
            {
                EnsureSchemaComponent(pt);
                ps = new OpenApiSchemaReference(pt.Name);
            }
            else
            {
                ps = InferPrimitiveSchema(pt);
            }

            var schemaAttr = p.GetCustomAttributes(inherit: false)
                              .Where(a => a.GetType().Name == nameof(OpenApiSchemaAttribute))
                              .Cast<object>()
                              .LastOrDefault() as OpenApiSchemaAttribute;
            ApplySchemaAttr(schemaAttr, ps);
            if (allowNull && ps is OpenApiSchema poss)
            {
                poss.Type |= JsonSchemaType.Null;
            }

            // If we have an instance and this property has a default value, and the schema is concrete,
            // populate schema.Default (unless already set by attribute)
            if (inst is not null && ps is OpenApiSchema concrete && concrete.Default is null)
            {
                try
                {
                    var val = p.GetValue(inst);
                    if (val is not null)
                    {
                        concrete.Default = ToNode(val);
                    }
                }
                catch { /* ignore */ }
            }
            obj.Properties[p.Name] = ps;
        }

        return obj;
    }

    private void BuildHeaders(Type t)
    {
        Document.Components!.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);

        // class-level
        var classAttrs = t.GetCustomAttributes(inherit: false)
                          .Where(a => a.GetType().Name == nameof(OpenApiHeaderAttribute))
                          .ToArray();
        foreach (var a in classAttrs)
        {
            var h = CreateHeaderFromAttribute(a);
            var name = GetNameOverride(a) ?? t.Name;
            Document.Components!.Headers![name] = h;
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
                Document.Components!.Headers![name] = h;
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
                Document.Components!.Headers![name] = h;
            }
        }
    }

    // Map PowerShell validation attributes on properties to OpenAPI schema constraints
    private static void ApplyPowerShellValidationAttributes(PropertyInfo p, IOpenApiSchema s)
    {
        if (s is not OpenApiSchema sc)
        {
            return; // constraints only applicable on a concrete schema, not a $ref proxy
        }

        foreach (var attr in p.GetCustomAttributes(inherit: false))
        {
            var atName = attr.GetType().Name;
            switch (atName)
            {
                case "ValidateRangeAttribute":
                    {
                        var min = attr.GetType().GetProperty("MinRange")?.GetValue(attr);
                        var max = attr.GetType().GetProperty("MaxRange")?.GetValue(attr);
                        if (min is not null)
                        {
                            sc.Minimum = min.ToString();
                        }
                        if (max is not null)
                        {
                            sc.Maximum = max.ToString();
                        }
                        break;
                    }
                case "ValidateLengthAttribute":
                    {
                        var minLen = attr.GetType().GetProperty("MinLength")?.GetValue(attr) as int?;
                        var maxLen = attr.GetType().GetProperty("MaxLength")?.GetValue(attr) as int?;
                        if (minLen.HasValue && minLen.Value >= 0)
                        {
                            sc.MinLength = minLen.Value;
                        }
                        if (maxLen.HasValue && maxLen.Value >= 0)
                        {
                            sc.MaxLength = maxLen.Value;
                        }
                        break;
                    }
                case "ValidateSetAttribute":
                    {
                        var vals = attr.GetType().GetProperty("ValidValues")?.GetValue(attr) as System.Collections.IEnumerable;
                        if (vals is not null)
                        {
                            var list = new List<JsonNode>();
                            foreach (var v in vals)
                            {
                                var node = ToNode(v);
                                if (node is not null)
                                {
                                    list.Add(node);
                                }
                            }
                            if (list.Count > 0)
                            {
                                var existing = sc.Enum?.ToList() ?? [];
                                existing.AddRange(list);
                                sc.Enum = existing;
                            }
                        }
                        break;
                    }
                case "ValidatePatternAttribute":
                    {
                        var pattern = attr.GetType().GetProperty("RegexPattern")?.GetValue(attr) as string;
                        if (!string.IsNullOrWhiteSpace(pattern))
                        {
                            sc.Pattern = pattern;
                        }
                        break;
                    }
                case "ValidateCountAttribute":
                    {
                        var minItems = attr.GetType().GetProperty("MinLength")?.GetValue(attr) as int?;
                        var maxItems = attr.GetType().GetProperty("MaxLength")?.GetValue(attr) as int?;
                        if (minItems.HasValue && minItems.Value >= 0)
                        {
                            sc.MinItems = minItems.Value;
                        }
                        if (maxItems.HasValue && maxItems.Value >= 0)
                        {
                            sc.MaxItems = maxItems.Value;
                        }
                        break;
                    }
                case "ValidateNotNullOrEmptyAttribute":
                    {
                        // If string → minLength=1; if array → minItems=1
                        if ((sc.Type & JsonSchemaType.String) == JsonSchemaType.String)
                        {
                            if (sc.MinLength is null or < 1)
                            {
                                sc.MinLength = 1;
                            }
                        }
                        else if ((sc.Type & JsonSchemaType.Array) == JsonSchemaType.Array)
                        {
                            if (sc.MinItems is null or < 1)
                            {
                                sc.MinItems = 1;
                            }
                        }
                        break;
                    }
                default:
                    break;
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

    private void BuildLinks(Type t)
    {
        Document.Components!.Links ??= new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);

        // Class-first pattern: instantiate and read well-known members
        object? inst;
        try { inst = Activator.CreateInstance(t); } catch { inst = null; }

        string? opId = null;
        string? opRef = null;
        string? description = null;
        object? parametersObj = null;
        object? requestBodyObj = null;
        object? serverObj = null;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var p in t.GetProperties(flags))
        {
            var name = p.Name;
            object? value = null;
            try { value = inst is not null ? p.GetValue(inst) : null; } catch { }

            switch (name)
            {
                case "OperationId":
                    opId = value as string; break;
                case "OperationRef":
                    opRef = value as string; break;
                case "Description":
                    description = value as string; break;
                case "Parameters":
                    parametersObj = value; break;
                case "RequestBody":
                    requestBodyObj = value; break;
                case "Server":
                    serverObj = value; break;
                default:
                    break;
            }
        }

        var link = new OpenApiLink();

        // Spec: operationId and operationRef are mutually exclusive. Prefer OperationId if both provided.
        if (!string.IsNullOrWhiteSpace(opId))
        {
            link.OperationId = opId;
        }
        else if (!string.IsNullOrWhiteSpace(opRef))
        {
            link.OperationRef = opRef;
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            link.Description = description;
        }

        // Parameters: Hashtable/IDictionary<string, object> of name -> runtime expression (string)
        if (parametersObj is System.Collections.IDictionary dict)
        {
            var paramProp = typeof(OpenApiLink).GetProperty("Parameters", BindingFlags.Public | BindingFlags.Instance);
            if (paramProp != null && paramProp.CanWrite)
            {
                var pt = paramProp.PropertyType;
                if (pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                {
                    var args = pt.GetGenericArguments();
                    var keyType = args[0];
                    var valType = args[1];
                    if (keyType == typeof(string))
                    {
                        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valType);
                        var instDict = Activator.CreateInstance(dictType) as System.Collections.IDictionary;
                        if (instDict is not null)
                        {
                            foreach (System.Collections.DictionaryEntry de in dict)
                            {
                                var k = de.Key?.ToString();
                                var v = de.Value as string;
                                if (string.IsNullOrWhiteSpace(k) || string.IsNullOrWhiteSpace(v)) { continue; }
                                var valueToSet = CreateRuntimeExpressionAnyWrapper(valType, v);
                                instDict[k!] = valueToSet;
                            }
                            paramProp.SetValue(link, instDict);
                        }
                    }
                }
            }
        }

        // RequestBody: runtime expression string or literal object (hashtable)
        if (requestBodyObj is string rbe && !string.IsNullOrWhiteSpace(rbe))
        {
            var rbProp = typeof(OpenApiLink).GetProperty("RequestBody", BindingFlags.Public | BindingFlags.Instance);
            if (rbProp != null && rbProp.CanWrite)
            {
                var rbt = rbProp.PropertyType;
                var valueToSet = CreateRuntimeExpressionAnyWrapper(rbt, rbe);
                rbProp.SetValue(link, valueToSet);
            }
        }
        else if (requestBodyObj is System.Collections.IDictionary rbDict)
        {
            var rbProp = typeof(OpenApiLink).GetProperty("RequestBody", BindingFlags.Public | BindingFlags.Instance);
            if (rbProp != null && rbProp.CanWrite)
            {
                var rbt = rbProp.PropertyType;
                var asm = rbt.Assembly;
                var any = BuildOpenApiAny(rbDict, asm);
                var wrapper = Activator.CreateInstance(rbt);
                if (wrapper is not null)
                {
                    var anyProp = rbt.GetProperty("Any", BindingFlags.Public | BindingFlags.Instance);
                    if (anyProp != null && anyProp.CanWrite && any is not null)
                    {
                        anyProp.SetValue(wrapper, any);
                        rbProp.SetValue(link, wrapper);
                    }
                }
            }
        }

        // Server: allow string URL or hashtable { Url, Description, Variables }
        if (serverObj is not null)
        {
            var server = BuildServer(serverObj);
            if (server is not null)
            {
                link.Server = server;
            }
        }

        // Component key is class name
        Document.Components!.Links[t.Name] = link;

        static OpenApiServer? BuildServer(object value)
        {
            if (value is string urlStr && !string.IsNullOrWhiteSpace(urlStr))
            {
                return new OpenApiServer { Url = urlStr };
            }

            if (value is System.Collections.IDictionary ht)
            {
                var srv = new OpenApiServer();
                foreach (System.Collections.DictionaryEntry de in ht)
                {
                    var key = de.Key?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(key)) { continue; }
                    switch (key)
                    {
                        case "Url":
                        case "url":
                            srv.Url = de.Value?.ToString();
                            break;
                        case "Description":
                        case "description":
                            srv.Description = de.Value?.ToString();
                            break;
                        case "Variables":
                        case "variables":
                            if (de.Value is System.Collections.IDictionary vars)
                            {
                                srv.Variables = new Dictionary<string, OpenApiServerVariable>(StringComparer.Ordinal);
                                foreach (System.Collections.DictionaryEntry v in vars)
                                {
                                    var varName = v.Key?.ToString();
                                    if (string.IsNullOrWhiteSpace(varName) || v.Value is not System.Collections.IDictionary vht) { continue; }
                                    var osv = new OpenApiServerVariable();
                                    foreach (System.Collections.DictionaryEntry ve in vht)
                                    {
                                        var vk = ve.Key?.ToString();
                                        if (string.Equals(vk, "Default", StringComparison.OrdinalIgnoreCase))
                                        {
                                            osv.Default = ve.Value?.ToString();
                                        }
                                        else if (string.Equals(vk, "Description", StringComparison.OrdinalIgnoreCase))
                                        {
                                            osv.Description = ve.Value?.ToString();
                                        }
                                        else if (string.Equals(vk, "Enum", StringComparison.OrdinalIgnoreCase) && ve.Value is System.Collections.IEnumerable enumSeq)
                                        {
                                            osv.Enum = [];
                                            foreach (var item in enumSeq)
                                            {
                                                if (item is null) { continue; }
                                                osv.Enum.Add(item.ToString()!);
                                            }
                                        }
                                    }
                                    srv.Variables[varName] = osv;
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
                return srv;
            }

            return null;
        }

        // Helper: create an instance of RuntimeExpressionAnyWrapper (or compatible) from a string
        static object CreateRuntimeExpressionAnyWrapper(Type wrapperType, string text)
        {
            // If it's already a string, just return the text
            if (wrapperType == typeof(string)) { return text; }

            // Try ctor(string) directly
            var ctorStr = wrapperType.GetConstructor([typeof(string)]);
            if (ctorStr is not null)
            {
                try { return ctorStr.Invoke([text]); } catch { /* ignore */ }
            }

            // Try to locate Microsoft.OpenApi.RuntimeExpression and set as Expression
            var asm = wrapperType.Assembly;
            var exprType = asm.GetType("Microsoft.OpenApi.RuntimeExpression");
            if (exprType is not null)
            {
                try
                {
                    // Prefer wrapper ctor(RuntimeExpression)
                    var ctorExpr = wrapperType.GetConstructor([exprType]);
                    if (ctorExpr is not null)
                    {
                        var expr = Activator.CreateInstance(exprType, [text]);
                        return ctorExpr.Invoke([expr!]);
                    }

                    // Otherwise set property 'Expression'
                    var exprProp = wrapperType.GetProperty("Expression", BindingFlags.Public | BindingFlags.Instance);
                    if (exprProp != null && exprProp.CanWrite)
                    {
                        var expr = Activator.CreateInstance(exprType, [text]);
                        var inst = Activator.CreateInstance(wrapperType)!;
                        exprProp.SetValue(inst, expr);
                        return inst;
                    }
                }
                catch { /* ignore and try Any */ }
            }

            // Fallback: set as Any using Microsoft.OpenApi.Any.OpenApiString
            var anyType = asm.GetType("Microsoft.OpenApi.Any.OpenApiString");
            if (anyType != null)
            {
                try
                {
                    var any = Activator.CreateInstance(anyType, [text]);
                    var anyProp = wrapperType.GetProperty("Any", BindingFlags.Public | BindingFlags.Instance);
                    if (anyProp != null && anyProp.CanWrite)
                    {
                        var inst = Activator.CreateInstance(wrapperType)!;
                        anyProp.SetValue(inst, any);
                        return inst;
                    }
                }
                catch { /* ignore */ }
            }

            // As a last resort, create an empty wrapper instance
            return Activator.CreateInstance(wrapperType)!;
        }

        // Build an OpenAPI Any object (OpenApiObject/OpenApiArray/OpenApiString) via reflection
        static object? BuildOpenApiAny(object value, Assembly asm)
        {
            var anyNs = "Microsoft.OpenApi.Any";
            var tObj = asm.GetType($"{anyNs}.OpenApiObject");
            var tArr = asm.GetType($"{anyNs}.OpenApiArray");
            var tStr = asm.GetType($"{anyNs}.OpenApiString");
            if (tStr is null) { return null; }

            // IDictionary -> OpenApiObject
            if (value is System.Collections.IDictionary d)
            {
                if (Activator.CreateInstance(tObj!) is not System.Collections.IDictionary obj) { return null; }
                foreach (System.Collections.DictionaryEntry de in d)
                {
                    var key = de.Key?.ToString();
                    if (string.IsNullOrWhiteSpace(key)) { continue; }
                    var child = BuildOpenApiAny(de.Value ?? string.Empty, asm) ?? Activator.CreateInstance(tStr, [string.Empty]);
                    obj[key] = child;
                }
                return obj;
            }

            // IEnumerable (non-string) -> OpenApiArray
            if (value is System.Collections.IEnumerable en and not string)
            {
                if (Activator.CreateInstance(tArr!) is not System.Collections.IList arr) { return null; }
                foreach (var item in en)
                {
                    var child = BuildOpenApiAny(item ?? string.Empty, asm) ?? Activator.CreateInstance(tStr, [string.Empty]);
                    _ = arr.Add(child);
                }
                return arr;
            }

            // Primitive -> OpenApiString(text)
            return Activator.CreateInstance(tStr, [value?.ToString() ?? string.Empty]);
        }
    }

    private void BuildCallbacks(Type t)
    {
        Document.Components!.Callbacks ??= new Dictionary<string, IOpenApiCallback>(StringComparer.Ordinal);

        // Instantiate the class to pull default values
        object? inst = null;
        try { inst = Activator.CreateInstance(t); } catch { }

        string? description = null;
        var expressions = new List<string>();
        OpenApiPathItem? providedPathItem = null;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var p in t.GetProperties(flags))
        {
            object? val = null;
            try { val = inst is not null ? p.GetValue(inst) : null; } catch { }

            switch (p.Name)
            {
                case "Description":
                    description = val as string; break;
                case "Expression":
                    if (val is string s && !string.IsNullOrWhiteSpace(s)) { expressions.Add(s); }
                    break;
                case "Expressions":
                    if (val is System.Collections.IEnumerable en)
                    {
                        foreach (var item in en)
                        {
                            if (item is string es && !string.IsNullOrWhiteSpace(es)) { expressions.Add(es); }
                        }
                    }
                    break;
                case "PathItem":
                    if (val is OpenApiPathItem pi) { providedPathItem = pi; }
                    break;
                default:
                    break;
            }
        }

        if (expressions.Count == 0)
        {
            // Nothing to build
            return;
        }

        var cb = new OpenApiCallback();
        // Build a minimal PathItem if one was not provided
        var pathItem = providedPathItem ?? new OpenApiPathItem { Description = description };

        // Resolve the exact IDictionary<string, OpenApiPathItem> interface and its Add method
        var dictIface = typeof(OpenApiCallback)
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType
                               && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                               && i.GetGenericArguments()[0] == typeof(string)
                               && i.GetGenericArguments()[1] == typeof(OpenApiPathItem));
        var addMethod = dictIface?.GetMethod("Add", [typeof(string), typeof(OpenApiPathItem)]);
        foreach (var expr in expressions.Distinct(StringComparer.Ordinal))
        {
            if (addMethod is not null)
            {
                _ = addMethod.Invoke(cb, [expr, pathItem]);
            }
            else
            {
                // Fallback: try IDictionary<string, object> add via dynamic as a last resort
                try { ((dynamic)cb).Add(expr, pathItem); } catch { /* ignore */ }
            }
        }

        Document.Components!.Callbacks[t.Name] = cb;
    }
}
