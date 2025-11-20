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
using Microsoft.OpenApi.Reader;
using System.Text;
using System.Management.Automation;
using Kestrun.Authentication;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public class OpenApiDocDescriptor
{
    /// <summary>
    /// The Kestrun host providing registered routes.
    /// </summary>
    public KestrunHost Host { get; init; }

    /// <summary>
    /// The ID of the OpenAPI document being generated.
    /// </summary>
    public string DocumentId { get; init; }

    /// <summary>
    /// The OpenAPI document being generated.
    /// </summary>
    public OpenApiDocument Document { get; private set; } = new OpenApiDocument { Components = new OpenApiComponents() };

    /// <summary>
    /// Security requirements for the OpenAPI document.
    /// </summary>
    public IDictionary<string, OpenApiSecurityRequirement> SecurityRequirement { get; private set; } = new Dictionary<string, OpenApiSecurityRequirement>();

    /// <summary>
    /// Initializes a new instance of the OpenApiDocDescriptor.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="docId">The ID of the OpenAPI document being generated.</param>
    /// <exception cref="ArgumentNullException">Thrown if host or docId is null.</exception>
    public OpenApiDocDescriptor(KestrunHost host, string docId)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(docId);
        Host = host;
        DocumentId = docId;
    }

    /// <summary>
    /// Generates an OpenAPI document from the provided schema types.
    /// </summary>
    /// <param name="components">The set of discovered OpenAPI component types.</param>
    /// <returns>The generated OpenAPI document.</returns>
    public void GenerateComponents(OpenApiComponentSet components)
    {
        Document.Components ??= new OpenApiComponents();
        // Examples
        if (components.ExampleTypes is not null && components.ExampleTypes.Count > 0)
        {
            Document.Components.Examples = new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
            foreach (var t in components.ExampleTypes)
            {
                BuildExamples(t);
            }
        }
        // Schemas
        if (components.SchemaTypes is not null && components.SchemaTypes.Count > 0)
        {
            Document.Components.Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
            foreach (var t in components.SchemaTypes)
            {
                BuildSchema(t);
            }
        }

        // Headers
        if (components.HeaderTypes is not null && components.HeaderTypes.Count > 0)
        {
            Document.Components.Headers = new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
            foreach (var t in components.HeaderTypes)
            {
                BuildHeaders(t);
            }
        }
        // Parameters
        if (components.ParameterTypes is not null && components.ParameterTypes.Count > 0)
        {
            Document.Components.Parameters = new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal);
            foreach (var t in components.ParameterTypes)
            {
                BuildParameters(t);
            }
        }
        // Links
        if (components.LinkTypes is not null && components.LinkTypes.Count > 0)
        {
            Document.Components.Links = new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
            foreach (var t in components.LinkTypes)
            {
                BuildLinks(t);
            }
        }
        // Callbacks
        if (components.CallbackTypes is not null && components.CallbackTypes.Count > 0)
        {
            Document.Components.Callbacks = new Dictionary<string, IOpenApiCallback>(StringComparer.Ordinal);
            foreach (var t in components.CallbackTypes)
            {
                BuildCallbacks(t);
            }
        }
        // Responses
        if (components.ResponseTypes is not null && components.ResponseTypes.Count > 0)
        {
            Document.Components.Responses = new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal);
            foreach (var t in components.ResponseTypes)
            {
                BuildResponses(t);
            }
        }
        // Request bodies
        if (components.RequestBodyTypes is not null && components.RequestBodyTypes.Count > 0)
        {
            Document.Components.RequestBodies = new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal);
            foreach (var t in components.RequestBodyTypes)
            {
                BuildRequestBodies(t);
            }
        }
    }

    /// <summary>
    /// Generates the OpenAPI document by auto-discovering component types.
    /// </summary>
    public void GenerateComponents()
    {
        var components = OpenApiSchemaDiscovery.GetOpenApiTypesAuto();
        GenerateComponents(components);
    }
    /// <summary>
    /// Generates the OpenAPI document by processing components and registered routes.
    /// </summary>
    public void GenerateDoc()
    {
        // First, generate components
        GenerateComponents();
        // Finally, build paths from registered routes
        BuildPathsFromRegisteredRoutes(Host.RegisteredRoutes);
    }

    /// <summary>
    /// Reads and diagnoses the OpenAPI document by serializing and re-parsing it.
    /// </summary>
    /// <param name="version">The OpenAPI specification version to read as.</param>
    /// <returns>A tuple containing the OpenAPI document and any diagnostics.</returns>
    public ReadResult ReadAndDiagnose(OpenApiSpecVersion version)
    {
        using var sw = new StringWriter();
        var w = new OpenApiJsonWriter(sw);
        Document.SerializeAs(version, w);
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(sw.ToString()));
        // format must be "json" or "yaml"
        return OpenApiDocument.Load(ms);
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
        Document.Paths = [];
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
                        dynamic dPath = pathItem;
                        if (dPath.Servers == null) { dPath.Servers = new List<OpenApiServer>(); }
                        foreach (var s in pathMeta.Servers)
                        {
                            dPath.Servers.Add(s);
                        }
                    }
                    if (pathMeta.Parameters is { Count: > 0 })
                    {
                        dynamic dPath = pathItem;
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
    private OpenApiOperation BuildOperationFromMetadata(OpenAPIMetadata meta)
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
        if (Document.Components?.SecuritySchemes is Dictionary<string, IOpenApiSecurityScheme> schemes)
        {
            if (meta.Security is not null)
            {
                // If meta.Security is an empty sequence, you may want to mark the op as anonymous.
                if (!meta.Security.Any())
                {
                    // Explicitly anonymous for this operation (overrides any Document.Security)
                    op.Security = [];
                }
                else
                {
                    op.Security ??= [];
                    // OR semantics: add one requirement per scheme name
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var schemeName in meta.Security)
                    {
                        if (!seen.Add(schemeName))
                        {
                            continue; // skip duplicates
                        }

                        if (!schemes.ContainsKey(schemeName))
                        {
                            continue; // or log/throw
                        }
                        if (SecurityRequirement.TryGetValue(schemeName, out var existingRequirement))
                        {
                            op.Security.Add(existingRequirement);
                        }
                    }
                }
            }
        }

        return op;
    }

    #region Schemas

    private static JsonSchemaType Map(OaSchemaType t) => t switch
    {
        OaSchemaType.String => JsonSchemaType.String,
        OaSchemaType.Integer => JsonSchemaType.Integer,
        OaSchemaType.Number => JsonSchemaType.Number,
        OaSchemaType.Boolean => JsonSchemaType.Boolean,
        _ => JsonSchemaType.String
    };
    private static OpenApiPropertyAttribute? GetSchemaIdentity(Type t)
    {
        // inherit:true already climbs the chain until it finds the first one
        var attrs = (OpenApiPropertyAttribute[])t.GetCustomAttributes(typeof(OpenApiPropertyAttribute), inherit: true);
        return attrs.Length > 0 ? attrs[0] : null;
    }

    private IOpenApiSchema BuildSchemaForType(Type t, HashSet<Type>? built = null)
    {
        built ??= [];
        if (t.BaseType is not null && t.BaseType != typeof(object))
        {
            if (typeof(IOpenApiType).IsAssignableFrom(t))
            {
                var a = GetSchemaIdentity(t);
                if (a is not null)
                {
                    return new OpenApiSchema
                    {
                        Type = Map(a.Type),
                        Format = a.Format
                    };
                }
            }
            else
            {
                return new OpenApiSchemaReference(t.BaseType.Name); // Ensure base type schema is built first
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

        var clsComp = t.GetCustomAttributes(inherit: false)
        .Where(a => a is OpenApiSchemaComponent)
        .OrderBy(a => a is not OpenApiSchemaComponent)
        .ToArray();
        foreach (var a in clsComp)
        {
            if (a is OpenApiSchemaComponent schemaAttribute)
            {
                // Use the Key as the component name if provided
                if (schemaAttribute.Title is not null)
                {
                    schema.Title = schemaAttribute.Title;
                }
                if (!string.IsNullOrWhiteSpace(schemaAttribute.Description))
                {
                    schema.Description = schemaAttribute.Description;
                }

                schema.Deprecated |= schemaAttribute.Deprecated;
                schema.AdditionalPropertiesAllowed |= schemaAttribute.AdditionalPropertiesAllowed;

                if (schemaAttribute.Example is not null)
                {
                    schema.Example = ToNode(schemaAttribute.Example);
                }
                if (schemaAttribute.Examples is not null)
                {
                    schema.Examples ??= [];
                    var node = ToNode(schemaAttribute.Examples);
                    if (node is not null)
                    {
                        schema.Examples.Add(node);
                    }
                }

                if (!string.IsNullOrWhiteSpace(schemaAttribute.Required))
                {
                    schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
                    var tmp = schemaAttribute.Required?
                     .Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
                    foreach (var r in tmp!)
                    {
                        _ = schema.Required.Add(r);
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
            schema.Properties[p.Name] = ps;
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
    private void BuildSchema(Type t, HashSet<Type>? built = null)
    {
        if (Document.Components is not null && Document.Components.Schemas is not null)
        {
            Document.Components.Schemas[t.Name] = BuildSchemaForType(t, built);
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
            ApplySchemaAttr(p.GetCustomAttribute<OpenApiPropertyAttribute>(), refSchema);
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
            var attrs = p.GetCustomAttributes<OpenApiPropertyAttribute>(inherit: false).ToArray();
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
            ApplySchemaAttr(p.GetCustomAttribute<OpenApiPropertyAttribute>(), s);
            ApplyPowerShellValidationAttributes(p, s);
            if (allowNull)
            {
                s.Type |= JsonSchemaType.Null;
            }
            return s;
        }

        // primitive
        var prim = InferPrimitiveSchema(pt);
        ApplySchemaAttr(p.GetCustomAttribute<OpenApiPropertyAttribute>(), prim);
        ApplyPowerShellValidationAttributes(p, prim);
        if (allowNull)
        {
            prim.Type |= JsonSchemaType.Null;
        }
        return prim;
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
    /// <param name="t">The .NET type to infer from.</param>
    /// <returns>The inferred OpenApiSchema.</returns>
    private static OpenApiSchema InferPrimitiveSchema(Type t)
    {
        if (t == typeof(string))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String };
        }

        if (t == typeof(bool))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Boolean };
        }

        // Integer types
        if (t == typeof(int) || t == typeof(short) || t == typeof(byte))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" };
        }
        if (t == typeof(long))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" };
        }

        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Number };
        }

        if (t == typeof(DateTime))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" };
        }

        if (t == typeof(object))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Object };
        }
        if (t == typeof(void))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Null };
        }
        if (t == typeof(char))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 1, MinLength = 1 };
        }
        if (t == typeof(sbyte) || t == typeof(ushort) || t == typeof(uint) || t == typeof(ulong))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Integer };
        }
        if (t == typeof(DateTimeOffset))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" };
        }
        if (t == typeof(TimeSpan))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "duration" };
        }
        if (t == typeof(byte[]))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "byte" };
        }
        if (t == typeof(Uri))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" };
        }
        if (t == typeof(Guid))
        {
            return new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" };
        }
        // Fallback
        return new OpenApiSchema { Type = JsonSchemaType.String };
    }

    private static void ApplySchemaAttr(OpenApiPropertyAttribute? a, IOpenApiSchema s)
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
            if (a.AdditionalProperties is not null)
            {
                sc.AdditionalProperties = new OpenApiSchemaReference(a.AdditionalProperties);
            }
            // nullable bool
            if (a.AdditionalPropertiesAllowed is not null)
            {
                sc.AdditionalPropertiesAllowed = (bool)a.AdditionalPropertiesAllowed;
            }
            sc.UnevaluatedProperties = a.UnevaluatedProperties;
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
        => t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(Guid) || t == typeof(object);
    #endregion

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
        return JsonValue.Create(value?.ToString() ?? string.Empty);
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

    #region Parameters

    /// <summary>
    /// Builds OpenAPI parameters from a given type's properties.
    /// </summary>
    /// <param name="t">The type to build parameters from.</param>
    /// <exception cref="InvalidOperationException">Thrown when the type has multiple [OpenApiResponseComponent] attributes.</exception>
    private void BuildParameters(Type t)
    {
        string? defaultDescription = null;
        string? joinClassName = null;
        Document.Components!.Parameters ??= new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal);

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var p in t.GetProperties(flags))
        {
            var parameter = new OpenApiParameter();
            var classAttrs = t.GetCustomAttributes(inherit: false).
                            Where(a => a.GetType().Name is
                            nameof(OpenApiParameterComponent))
                            .Cast<object>()
                            .ToArray();
            if (classAttrs.Length > 0)
            {
                if (classAttrs.Length > 1)
                {
                    throw new InvalidOperationException($"Type '{t.FullName}' has multiple [OpenApiResponseComponent] attributes. Only one is allowed per class.");
                }
                // Apply any class-level [OpenApiResponseComponent] attributes first
                if (classAttrs[0] is OpenApiParameterComponent classRespAttr)
                {
                    if (!string.IsNullOrEmpty(classRespAttr.Description))
                    {
                        defaultDescription = classRespAttr.Description;
                    }
                    if (!string.IsNullOrEmpty(classRespAttr.JoinClassName))
                    {
                        joinClassName = t.FullName + classRespAttr.JoinClassName;
                    }
                }
            }

            var attrs = p.GetCustomAttributes(inherit: false)
                         .Where(a => a.GetType().Name is
                         (nameof(OpenApiParameterAttribute)) or
                         (nameof(OpenApiPropertyAttribute)) or
                         (nameof(OpenApiExampleRefAttribute))
                         )
                         .Cast<object>()
                         .ToArray();

            if (attrs.Length == 0) { continue; }
            var hasResponseDef = false;
            var customName = string.Empty;
            foreach (var a in attrs)
            {
                if (a is OpenApiParameterAttribute oaRa)
                {
                    if (!string.IsNullOrWhiteSpace(oaRa.Key))
                    {
                        customName = oaRa.Key;
                    }
                }
                if (CreateParameterFromAttribute(a, parameter))
                {
                    hasResponseDef = true;
                }
            }
            if (!hasResponseDef)
            {
                continue;
            }
            var tname = string.IsNullOrWhiteSpace(customName) ? p.Name : customName!;
            var key = joinClassName is not null ? $"{joinClassName}{tname}" : tname;
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                parameter.Name = tname;
            }
            if (parameter.Description is null && defaultDescription is not null)
            {
                parameter.Description = defaultDescription;
            }
            Document.Components.Parameters[key] = parameter;

            var schemaAttr = (OpenApiPropertyAttribute?)p.GetCustomAttributes(inherit: false)
                              .Where(a => a.GetType().Name == "OpenApiPropertyAttribute")
                              .Cast<object>()
                              .LastOrDefault();
            // Build and assign the parameter schema
            parameter.Schema = CreatePropertySchema(schemaAttr, t, p);
        }
    }

    private IOpenApiSchema CreatePropertySchema(OpenApiPropertyAttribute? schemaAttr, Type t, PropertyInfo p)
    {
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
            ApplySchemaAttr(schemaAttr, s);
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
            ApplySchemaAttr(schemaAttr, s);
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
            ApplySchemaAttr(schemaAttr, r);
            paramSchema = r;
        }
        // PRIMITIVE
        else
        {
            var s = InferPrimitiveSchema(pt);
            ApplySchemaAttr(schemaAttr, s);
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
                catch { }
            }
            if (allowNull)
            {
                s.Type |= JsonSchemaType.Null;
            }
            paramSchema = s;
        }
        return paramSchema;
    }

    private bool CreateParameterFromAttribute(object attr, OpenApiParameter parameter)
    {
        switch (attr)
        {
            case OpenApiParameterAttribute param:

                parameter.Description = param.Description;
                parameter.Name = string.IsNullOrEmpty(param.Name) ? param.Key : param.Name;
                parameter.Required = param.Required;
                parameter.Deprecated = param.Deprecated;
                parameter.AllowEmptyValue = param.AllowEmptyValue;
                if (param.Explode)
                {
                    parameter.Explode = param.Explode;
                }
                parameter.AllowReserved = param.AllowReserved;
                if (!string.IsNullOrEmpty(param.In))
                {
                    parameter.In = param.In.ToOpenApiParameterLocation();
                    if (parameter.In == ParameterLocation.Path)
                    {
                        parameter.Required = true; // path parameters must be required
                    }
                }

                if (param.Style is not null)
                {
                    parameter.Style = param.Style.ToParameterStyle();
                }
                if (param.Example is not null)
                {
                    parameter.Example = ToNode(param.Example);
                }
                break;

            case OpenApiExampleRefAttribute exRef:
                parameter.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
                if (exRef.Inline)
                {
                    if (Document.Components?.Examples == null || !Document.Components.Examples.TryGetValue(exRef.ReferenceId, out var value))
                    {
                        throw new InvalidOperationException($"Example reference '{exRef.ReferenceId}' cannot be embedded because it was not found in components.");
                    }
                    if (value is not OpenApiExample example)
                    {
                        throw new InvalidOperationException($"Example reference '{exRef.ReferenceId}' cannot be embedded because it is not an OpenApiExample.");
                    }
                    parameter.Examples[exRef.Key] = example.Clone();
                }
                else
                {
                    parameter.Examples[exRef.Key] = new OpenApiExampleReference(exRef.ReferenceId);
                }
                break;

            default:
                return false; // unrecognized attribute type
        }
        return true;
    }
    // ---- local helpers ----

    #endregion

    #region Responses
    /// <summary>
    /// Builds response components from the specified type.
    /// </summary>
    /// <param name="t">The type to build responses for.</param>
    private void BuildResponses(Type t)
    {
        string? defaultDescription = null;
        string? joinClassName = null;
        // Ensure Responses dictionary exists
        Document.Components!.Responses ??= new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal);

        // Scan properties for response-related attributes
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var p in t.GetProperties(flags))
        {
            var response = new OpenApiResponse();
            var classAttrs = t.GetCustomAttributes(inherit: false).
                                Where(a => a.GetType().Name is
                                nameof(OpenApiResponseComponent))
                                .Cast<object>()
                                .ToArray();
            if (classAttrs.Length > 0)
            {
                if (classAttrs.Length > 1)
                {
                    throw new InvalidOperationException($"Type '{t.FullName}' has multiple [OpenApiResponseComponent] attributes. Only one is allowed per class.");
                }
                // Apply any class-level [OpenApiResponseComponent] attributes first
                if (classAttrs[0] is OpenApiResponseComponent classRespAttr)
                {
                    if (!string.IsNullOrEmpty(classRespAttr.Description))
                    {
                        defaultDescription = classRespAttr.Description;
                    }
                    if (!string.IsNullOrEmpty(classRespAttr.JoinClassName))
                    {
                        joinClassName = t.FullName + classRespAttr.JoinClassName;
                    }
                }
            }
            var attrs = p.GetCustomAttributes(inherit: false)
                         .Where(a => a.GetType().Name is
                         (nameof(OpenApiResponseAttribute)) or
                         (nameof(OpenApiLinkRefAttribute)) or
                         (nameof(OpenApiHeaderRefAttribute)) or
                         (nameof(OpenApiContentTypeAttribute)) or
                         (nameof(OpenApiExampleRefAttribute)))
                         .Cast<object>()
                         .ToArray();

            if (attrs.Length == 0) { continue; }
            var hasResponseDef = false;
            var customName = string.Empty;
            // Support multiple attributes per property
            foreach (var a in attrs)
            {
                if (a is OpenApiResponseAttribute oaRa)
                {
                    if (!string.IsNullOrWhiteSpace(oaRa.Key))
                    {
                        customName = oaRa.Key;
                    }
                }
                if (CreateResponseFromAttribute(a, response))
                {
                    hasResponseDef = true;
                }
            }
            if (!hasResponseDef)
            {
                continue;
            }
            var tname = string.IsNullOrWhiteSpace(customName) ? p.Name : customName!;
            var key = joinClassName is not null ? $"{joinClassName}{tname}" : tname;
            if (response.Description is null && defaultDescription is not null)
            {
                response.Description = defaultDescription;
            }
            // Apply default description if none set
            Document.Components!.Responses![key] = response;
            // Skip inferring schema/content for object-typed properties
            if (p.PropertyType.Name == "Object") { continue; }

            // If no schema/content was defined via attributes, infer from property type
            response.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                ["application/json"] = new OpenApiMediaType()
            };
            var pt = p.PropertyType;
            IOpenApiSchema iSchema;
            var allowNull = false;
            var underlying = Nullable.GetUnderlyingType(pt);
            if (underlying != null)
            {
                allowNull = true;
                pt = underlying;
            }

            if (pt.IsEnum)
            {
                var schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [.. pt.GetEnumNames().Select(n => (JsonNode)n)]
                };
                var propAttrs = p.GetCustomAttributes<OpenApiPropertyAttribute>(inherit: false).ToArray();
                var a = MergeSchemaAttributes(propAttrs);
                ApplySchemaAttr(a, schema);
                ApplyPowerShellValidationAttributes(p, schema);
                if (allowNull)
                {
                    schema.Type |= JsonSchemaType.Null;
                }
                iSchema = schema;
            }
            else if (pt.IsArray)
            {
                var item = pt.GetElementType()!;
                IOpenApiSchema itemSchema;

                if (!IsPrimitiveLike(item) && !item.IsEnum)
                {
                    // then reference it
                    itemSchema = new OpenApiSchemaReference(item.Name);
                }
                else
                {
                    itemSchema = InferPrimitiveSchema(item);
                }
                var schema = new OpenApiSchema
                {
                    // then build the array schema
                    Type = JsonSchemaType.Array,
                    Items = itemSchema
                };
                ApplySchemaAttr(p.GetCustomAttribute<OpenApiPropertyAttribute>(), schema);
                ApplyPowerShellValidationAttributes(p, schema);
                if (allowNull)
                {
                    schema.Type |= JsonSchemaType.Null;
                }
                iSchema = schema;
            }
            else if (!IsPrimitiveLike(pt))
            {
                EnsureSchemaComponent(pt);

                iSchema = new OpenApiSchemaReference(pt.Name);
            }
            else
            {
                var schema = InferPrimitiveSchema(pt);
                ApplySchemaAttr(p.GetCustomAttribute<OpenApiPropertyAttribute>(), schema);
                ApplyPowerShellValidationAttributes(p, schema);
                if (allowNull)
                {
                    schema.Type |= JsonSchemaType.Null;
                }
                iSchema = schema;
            }

            // Set schema to $ref of property type
            foreach (var a in response.Content.Values)
            {
                a.Schema = iSchema;
            }
        }
    }

    /// <summary>
    /// Gets the name override from an attribute, if present.
    /// </summary>
    /// <param name="attr">The attribute to inspect.</param>
    /// <returns>The name override, if present; otherwise, null.</returns>
    private static string? GetKeyOverride(object attr)
    {
        var t = attr.GetType();
        return t.GetProperty("Key")?.GetValue(attr) as string;
    }

    private bool CreateResponseFromAttribute(object attr, OpenApiResponse response)
    {
        ArgumentNullException.ThrowIfNull(attr);
        ArgumentNullException.ThrowIfNull(response);

        switch (attr)
        {
            case OpenApiContentTypeAttribute ctype:
                {
                    var media = GetOrAddMediaType(response, ctype.ContentType);

                    if (!string.IsNullOrEmpty(ctype.ReferenceId))
                    {
                        media.Schema = ctype.Inline
                            ? CloneSchemaOrThrow(ctype.ReferenceId)
                            : new OpenApiSchemaReference(ctype.ReferenceId);
                    }
                    return true;
                }

            case OpenApiResponseAttribute resp:
                {
                    if (!string.IsNullOrEmpty(resp.Description))
                    {
                        response.Description = resp.Description;
                    }
                    if (resp.SchemaRef is not null)
                    {
                        response.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal);
                        var media = GetOrAddMediaType(response, "application/json");
                        media.Schema = resp.Inline
                            ? CloneSchemaOrThrow(resp.SchemaRef)
                            : new OpenApiSchemaReference(resp.SchemaRef);
                    }
                    return true;
                }

            case OpenApiHeaderRefAttribute href:
                {
                    (response.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal))
                        [href.Key] = new OpenApiHeaderReference(href.ReferenceId);
                    return true;
                }

            case OpenApiLinkRefAttribute lref:
                {
                    (response.Links ??= new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal))
                        [lref.Key] = new OpenApiLinkReference(lref.ReferenceId);
                    return true;
                }

            case OpenApiExampleRefAttribute exRef:
                {
                    var targets =
                        exRef.ContentType is null
                            ? (IEnumerable<string>)(response.Content?.Keys ?? Array.Empty<string>())
                            : [exRef.ContentType];

                    if (!targets.Any())
                    {
                        targets = ["application/json"];
                    }

                    foreach (var ct in targets)
                    {
                        var media = GetOrAddMediaType(response, ct);
                        media.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
                        if (exRef.Inline)
                        {
                            if (Document.Components?.Examples == null || !Document.Components.Examples.TryGetValue(exRef.ReferenceId, out var value))
                            {
                                throw new InvalidOperationException($"Example reference '{exRef.ReferenceId}' cannot be embedded because it was not found in components.");
                            }
                            if (value is not OpenApiExample example)
                            {
                                throw new InvalidOperationException($"Example reference '{exRef.ReferenceId}' cannot be embedded because it is not an OpenApiExample.");
                            }
                            media.Examples[exRef.Key] = example.Clone();
                        }
                        else
                        {
                            media.Examples[exRef.Key] = new OpenApiExampleReference(exRef.ReferenceId);
                        }
                    }

                    return true;
                }
            default:
                return false;
        }
    }
    // --- local helpers -------------------------------------------------------

    private OpenApiMediaType GetOrAddMediaType(OpenApiResponse resp, string contentType)
    {
        resp.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal);
        if (!resp.Content.TryGetValue(contentType, out var media))
        {
            media = resp.Content[contentType] = new OpenApiMediaType();
        }

        return media;
    }

    private OpenApiSchema CloneSchemaOrThrow(string refId)
    {
        if (Document.Components?.Schemas is { } schemas &&
            schemas.TryGetValue(refId, out var schema))
        {
            // your existing clone semantics
            return (OpenApiSchema)schema.Clone();
        }

        throw new InvalidOperationException(
            $"Schema reference '{refId}' cannot be embedded because it was not found in components.");
    }

    #endregion

    #region Examples

    /// <summary>
    /// Builds example components from the specified type.
    /// </summary>
    /// <param name="t">The type to build examples from.</param>
    private void BuildExamples(Type t)
    {
        // Ensure Examples dictionary exists
        Document.Components!.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);

        // class-level
        var classAttrs = t.GetCustomAttributes(inherit: false)
                          .Where(a => a.GetType().Name == nameof(OpenApiExampleComponent))
                          .ToArray();
        foreach (var a in classAttrs)
        {
            string? customName = null;
            if (a is OpenApiExampleComponent oaEa)
            {
                if (!string.IsNullOrWhiteSpace(oaEa.Key))
                {
                    customName = oaEa.Key;
                }
            }
            var name = customName ?? t.Name;
            if (!Document.Components!.Examples!.ContainsKey(name))
            {
                var ex = CreateExampleFromAttribute(a);

                var inst = Activator.CreateInstance(t);
                ex.Value ??= ToNode(inst);
                Document.Components!.Examples![name] = ex;
            }
        }
    }

    /// <summary>
    /// Creates an OpenApiExample from the specified attribute.
    /// </summary>
    /// <param name="attr">The attribute object.</param>
    /// <returns>The created OpenApiExample.</returns>
    private static OpenApiExample CreateExampleFromAttribute(object attr)
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

    #endregion
    #region Headers
    /// <summary>
    /// Builds header components from the specified type.
    /// </summary>
    /// <param name="t">The type to build headers from.</param>
    /// <exception cref="InvalidOperationException">Thrown when the type has multiple [OpenApiHeaderComponent] attributes.</exception>
    private void BuildHeaders(Type t)
    {
        string? defaultDescription = null;
        string? joinClassName = null;
        // Ensure Headers dictionary exists
        Document.Components!.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var p in t.GetProperties(flags))
        {
            var header = new OpenApiHeader();

            var classAttrs = t.GetCustomAttributes(inherit: false).
                               Where(a => a.GetType().Name is
                               nameof(OpenApiHeaderComponent))
                               .Cast<object>()
                               .ToArray();
            if (classAttrs.Length > 0)
            {
                // Apply any class-level [OpenApiResponseComponent] attributes first
                if (classAttrs[0] is OpenApiHeaderComponent classRespAttr)
                {
                    if (!string.IsNullOrEmpty(classRespAttr.Description))
                    {
                        defaultDescription = classRespAttr.Description;
                    }
                    if (!string.IsNullOrEmpty(classRespAttr.JoinClassName))
                    {
                        joinClassName = t.FullName + classRespAttr.JoinClassName;
                    }
                }
            }
            var attrs = p.GetCustomAttributes(inherit: false)
                                   .Where(a => a is
                                   OpenApiHeaderAttribute or
                                   OpenApiExampleRefAttribute or
                                   OpenApiExampleAttribute
                                   )
                                   .Cast<object>()
                                   .ToArray();

            if (attrs.Length == 0) { continue; }
            var customName = string.Empty;
            foreach (var a in attrs)
            {
                if (a is OpenApiHeaderAttribute oaHa)
                {
                    if (!string.IsNullOrWhiteSpace(oaHa.Key))
                    {
                        customName = oaHa.Key;
                    }
                }
                _ = CreateHeaderFromAttribute(a, header);
            }
            var tname = string.IsNullOrWhiteSpace(customName) ? p.Name : customName!;
            var key = joinClassName is not null ? $"{joinClassName}{tname}" : tname;
            if (header.Description is null && defaultDescription is not null)
            {
                header.Description = defaultDescription;
            }
            Document.Components!.Headers![key] = header;
        }
    }

    /// <summary>
    /// Creates an OpenApiHeader from the specified attribute.
    /// </summary>
    /// <param name="attr">The attribute to create the header from.</param>
    /// <param name="header">The OpenApiHeader to populate.</param>
    /// <returns>True if the header was created successfully; otherwise, false.</returns>
    private static bool CreateHeaderFromAttribute(object attr, OpenApiHeader header)
    {
        if (attr is OpenApiHeaderAttribute attribute)
        {
            // Populate header fields
            header.Description = attribute.Description;
            header.Required = attribute.Required;
            header.Deprecated = attribute.Deprecated;
            header.AllowEmptyValue = attribute.AllowEmptyValue;
            header.Schema = string.IsNullOrWhiteSpace(attribute.SchemaRef) ? new OpenApiSchema { Type = JsonSchemaType.String } : new OpenApiSchemaReference(attribute.SchemaRef);
            // Optional hints
            header.Style = attribute.Style.ToOpenApi();

            header.AllowReserved = attribute.AllowReserved;

            header.Explode = attribute.Explode;
            var example = attribute.Example;
            if (example is not null)
            {
                header.Example = ToNode(example);
            }
        }
        else if (attr is OpenApiExampleRefAttribute exRef)
        {
            header.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
            if (header.Examples.ContainsKey(exRef.Key))
            {
                throw new InvalidOperationException($"Header already contains an example with the key '{exRef.Key}'.");
            }
            // Determine which content types to add the example reference to
            header.Examples[exRef.Key] = new OpenApiExampleReference(exRef.ReferenceId);
        }
        else if (attr is OpenApiExampleAttribute ex)
        {
            if (ex.Key is null)
            {
                throw new InvalidOperationException($"OpenApiExampleAttribute requires a non-null Name property.");
            }

            header.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
            if (header.Examples.ContainsKey(ex.Key))
            {
                throw new InvalidOperationException($"Header already contains an example with the key '{ex.Key}'.");
            }
            // Determine which content types to add the example reference to
            header.Examples[ex.Key] = new OpenApiExample()
            {
                Summary = ex.Summary,
                Description = ex.Description,
                Value = ToNode(ex.Value),
                ExternalValue = ex.ExternalValue
            };
        }
        else
        {
            return false; // unrecognized attribute type
        }
        return true;
    }

    #endregion

    #region RequestBodies
    /// <summary>
    /// Builds request body components from the specified type.
    /// </summary>
    /// <param name="t">The type to build request bodies for.</param>
    private void BuildRequestBodies(Type t)
    {
        Document.Components!.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal);
        var schema = BuildSchemaForType(t);
        var requestBody = new OpenApiRequestBody();
        // class-level
        var classAttrs = t.GetCustomAttributes(inherit: false)
            .Where(a => a is OpenApiRequestBodyComponent or OpenApiExampleRefAttribute or OpenApiPropertyAttribute)
            .OrderBy(a => a is not OpenApiRequestBodyComponent)
            .ToArray();

        var name = string.Empty;
        foreach (var a in classAttrs)
        {
            OpenApiMediaType mediaType;
            switch (a)
            {
                case OpenApiRequestBodyComponent bodyAttribute:

                    name = GetKeyOverride(a) ?? t.Name;
                    if (bodyAttribute.Description is not null)
                    {
                        requestBody.Description = bodyAttribute.Description;
                    }
                    requestBody.Required |= bodyAttribute.Required;
                    // Build content
                    requestBody.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal);
                    mediaType = new OpenApiMediaType
                    {
                        Schema = schema //new OpenApiSchemaReference(name)
                    };

                    if (bodyAttribute.Example is not null)
                    {
                        mediaType.Example = ToNode(bodyAttribute.Example);
                    }
                    requestBody.Content[bodyAttribute.ContentType ?? "application/json"] = mediaType;
                    break;
                case OpenApiPropertyAttribute schemaAttr:
                    if (schemaAttr.Array && schema is OpenApiSchemaReference)
                    {
                        // Wrap referenced schema in an array schema
                        var arraySchema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Array,
                            Items = schema
                        };
                        schema = arraySchema;
                    }
                    // Apply schema attribute to the schema
                    ApplySchemaAttr(schemaAttr, schema);
                    // No content yet; create default application/json media type
                    requestBody.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
                    {
                        ["application/json"] = new OpenApiMediaType()
                    };
                    // Determine which content types to add the example reference to
                    foreach (var value in requestBody.Content.Values)
                    {
                        value.Schema = schema;
                    }
                    break;
                case OpenApiExampleRefAttribute exRef:

                    requestBody.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal);
                    // Determine which content types to add the example reference to
                    var keys = (exRef.ContentType is null) ?
                        requestBody.Content.Keys : [exRef.ContentType];
                    if (keys.Count == 0)
                    {
                        // No existing content types; default to application/json
                        keys = ["application/json"];
                    }
                    // Add example reference to each specified content type
                    foreach (var ct in keys)
                    {
                        _ = requestBody.Content.TryAdd(ct, new OpenApiMediaType());
                        mediaType = requestBody.Content[ct];
                        mediaType.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
                        IOpenApiExample exRefType;
                        if (exRef.Inline)
                        {
                            if (Document.Components?.Examples == null || !Document.Components.Examples.TryGetValue(exRef.ReferenceId, out var value))
                            {
                                throw new InvalidOperationException($"Example reference '{exRef.ReferenceId}' cannot be embedded because it was not found in components.");
                            }
                            if (value is not OpenApiExample example)
                            {
                                throw new InvalidOperationException($"Example reference '{exRef.ReferenceId}' cannot be embedded because it is not an OpenApiExample.");
                            }
                            exRefType = example.Clone();
                        }
                        else
                        {
                            exRefType = new OpenApiExampleReference(exRef.ReferenceId);
                        }
                        mediaType.Examples[exRef.Key] = exRefType;
                    }
                    break;
            }
        }
        Document.Components!.RequestBodies![name] = requestBody;
        return;
    }

    #endregion

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
                              .Where(a => a.GetType().Name == nameof(OpenApiPropertyAttribute))
                              .Cast<object>()
                              .LastOrDefault() as OpenApiPropertyAttribute;
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
    #region Links
    /// <summary>
    /// Builds link components from the specified type.
    /// </summary>
    /// <param name="t">The type to build links for.</param>
    private void BuildLinks(Type t)
    {
        string? defaultDescription = null;
        string? joinClassName = null;
        // Ensure Links dictionary exists
        Document.Components!.Links ??= new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        // ------ Build Links -------
        foreach (var p in t.GetProperties(flags))
        {
            var link = new OpenApiLink();

            var classAttrs = t.GetCustomAttributes(inherit: false).
                               Where(a => a.GetType().Name is
                               nameof(OpenApiLinkComponent))
                               .Cast<object>()
                               .ToArray();
            if (classAttrs.Length > 0)
            {
                // Apply any class-level [OpenApiLinkComponent] attributes first
                if (classAttrs[0] is OpenApiLinkComponent classLinkAttr)
                {
                    if (!string.IsNullOrEmpty(classLinkAttr.Description))
                    {
                        defaultDescription = classLinkAttr.Description;
                    }
                    if (!string.IsNullOrEmpty(classLinkAttr.JoinClassName))
                    {
                        joinClassName = t.FullName + classLinkAttr.JoinClassName;
                    }
                }
            }

            var attrs = p.GetCustomAttributes(inherit: false)
                                  .Where(a => a is OpenApiLinkAttribute or
                                    OpenApiServerAttribute or
                                    OpenApiServerVariableAttribute)
                                  .Cast<object>()
                                  .ToArray();

            if (attrs.Length == 0) { continue; }
            var customName = string.Empty;
            foreach (var a in attrs)
            {
                if (a is OpenApiLinkAttribute oaHa)
                {
                    if (!string.IsNullOrWhiteSpace(oaHa.Key))
                    {
                        customName = oaHa.Key;
                    }
                }
                _ = CreateLinkFromAttribute(a, link);
            }
            var tname = string.IsNullOrWhiteSpace(customName) ? p.Name : customName!;
            var key = joinClassName is not null ? $"{joinClassName}{tname}" : tname;
            if (link.Description is null && defaultDescription is not null)
            {
                link.Description = defaultDescription;
            }
            Document.Components!.Links![key] = link;
        }
    }

    /// <summary>
    /// Creates an OpenApiLink from the specified attribute.
    /// </summary>
    /// <param name="attr">The attribute to create the link from.</param>
    /// <param name="link">The link to populate.</param>
    /// <returns>True if the link was successfully created; otherwise, false.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the attribute is invalid.</exception>
    private static bool CreateLinkFromAttribute(object attr, OpenApiLink link)
    {
        if (attr is OpenApiLinkAttribute attribute)
        {
            if (!string.IsNullOrWhiteSpace(attribute.OperationId) &&
               !string.IsNullOrWhiteSpace(attribute.OperationRef))
            {
                throw new InvalidOperationException("OpenApiLinkAttribute cannot have both OperationId and OperationRef specified.");
            }
            if (!string.IsNullOrWhiteSpace(attribute.RequestBodyExpression) &&
                !string.IsNullOrWhiteSpace(attribute.RequestBodyJson))
            {
                throw new InvalidOperationException("OpenApiLinkAttribute cannot have both RequestBodyExpression and RequestBodyJson specified.");
            }
            // Populate link fields
            if (!string.IsNullOrWhiteSpace(attribute.Description))
            {
                link.Description = attribute.Description;
            }
            if (!string.IsNullOrWhiteSpace(attribute.OperationId))
            {
                if (link.OperationRef is not null)
                {
                    throw new InvalidOperationException("OpenApiLink cannot have both OperationId and OperationRef specified.");
                }
                link.OperationId = attribute.OperationId;
            }
            if (!string.IsNullOrWhiteSpace(attribute.OperationRef))
            {
                if (link.OperationId is not null)
                {
                    throw new InvalidOperationException("OpenApiLink cannot have both OperationId and OperationRef specified.");
                }
                link.OperationRef = attribute.OperationRef;
            }
            if (!string.IsNullOrWhiteSpace(attribute.MapKey) && !string.IsNullOrWhiteSpace(attribute.MapValue))
            {
                link.Parameters ??= new Dictionary<string, RuntimeExpressionAnyWrapper>(StringComparer.Ordinal);

                link.Parameters[attribute.MapKey] = new RuntimeExpressionAnyWrapper()
                {
                    Expression = RuntimeExpression.Build(attribute.MapValue)
                };
            }
            if (!string.IsNullOrWhiteSpace(attribute.RequestBodyExpression))
            {
                link.RequestBody = new RuntimeExpressionAnyWrapper()
                {
                    Expression = RuntimeExpression.Build(attribute.RequestBodyExpression)
                };
            }
            if (!string.IsNullOrWhiteSpace(attribute.RequestBodyJson))
            {
                link.RequestBody = new RuntimeExpressionAnyWrapper()
                {
                    Any = ToNode(attribute.RequestBodyJson)
                };
            }
            return true;
        }
        else if (attr is OpenApiServerAttribute server)
        {
            link.Server ??= new OpenApiServer();
            if (!string.IsNullOrWhiteSpace(server.Description))
            {
                link.Server.Description = server.Description;
            }
            link.Server.Url = server.Url;
            return true;
        }
        else if (attr is OpenApiServerVariableAttribute serverVariable)
        {
            if (string.IsNullOrWhiteSpace(serverVariable.Name))
            {
                throw new InvalidOperationException("OpenApiServerVariableAttribute requires a non-empty Name property.");
            }
            link.Server ??= new OpenApiServer();
            link.Server.Variables ??= new Dictionary<string, OpenApiServerVariable>(StringComparer.Ordinal);
            var osv = new OpenApiServerVariable();
            if (!string.IsNullOrWhiteSpace(serverVariable.Default))
            {
                osv.Default = serverVariable.Default;
            }
            if (!string.IsNullOrWhiteSpace(serverVariable.Description))
            {
                osv.Description = serverVariable.Description;
            }
            if (serverVariable.Enum is not null && serverVariable.Enum.Length > 0)
            {
                osv.Enum = [.. serverVariable.Enum];
            }
            link.Server.Variables[serverVariable.Name] = osv;
            return true;
        }
        return false;
    }

    #endregion

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

    /// <summary>
    /// Enumerates all in-session PowerShell functions in the given runspace,
    /// detects those annotated with [OpenApiPath], and maps them into the provided KestrunHost.
    /// </summary>
    /// <param name="cmdInfos">List of FunctionInfo objects representing PowerShell functions.</param>
    public void LoadAnnotatedFunctions(List<FunctionInfo> cmdInfos)
    {
        ArgumentNullException.ThrowIfNull(cmdInfos);

        foreach (var func in cmdInfos)
        {
            var sb = func.ScriptBlock;
            var openApiAttr = new OpenAPIMetadata();
            if (sb is null)
            {
                continue;
            }

            // Collect any [OpenApiPath] attributes placed before param()
            // Note: In C#, the attribute class is typically OpenApiPathAttribute; PowerShell allows [OpenApiPath] shorthand.
            var attrs = sb.Attributes;
            if (attrs.Count == 0)
            {
                continue;
            }
            var parsedVerb = HttpVerb.Get; // default
            var routeOptions = new MapRouteOptions();

            foreach (var attr in attrs)
            {
                if (attr is OpenApiPath oaPath)
                {
                    // HTTP Verb
                    var httpVerb = oaPath.HttpVerb ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(httpVerb))
                    {
                        parsedVerb = HttpVerbExtensions.FromMethodString(httpVerb);
                        routeOptions.HttpVerbs.Add(parsedVerb);
                    }

                    // Pattern
                    if (!string.IsNullOrWhiteSpace(oaPath.Pattern))
                    {
                        routeOptions.Pattern = oaPath.Pattern;
                        openApiAttr.Pattern = oaPath.Pattern;
                    }

                    // Summary
                    if (!string.IsNullOrWhiteSpace(oaPath.Summary))
                    {
                        openApiAttr.Summary = oaPath.Summary;
                    }

                    // Description
                    if (!string.IsNullOrWhiteSpace(oaPath.Description))
                    {
                        openApiAttr.Description = oaPath.Description;
                    }

                    // Tags
                    if (!string.IsNullOrWhiteSpace(oaPath.Tags))
                    {
                        openApiAttr.Tags = [.. oaPath.Tags.Split(',')];
                    }

                    // OperationId
                    if (!string.IsNullOrWhiteSpace(oaPath.OperationId))
                    {
                        openApiAttr.OperationId = oaPath.OperationId;
                    }
                    // Deprecated flag (per-verb OpenAPI metadata)
                    openApiAttr.Deprecated |= oaPath.Deprecated; // carry forward deprecated flag
                }
                else if (attr is OpenApiResponseRefAttribute oaRRa)
                {
                    openApiAttr.Responses ??= [];
                    IOpenApiResponse response;
                    // Determine if we inline the referenced response or use a $ref
                    if (oaRRa.Inline)
                    {
                        if (Document.Components?.Responses == null || !Document.Components.Responses.TryGetValue(oaRRa.ReferenceId, out var value))
                        {
                            throw new InvalidOperationException($"Response reference '{oaRRa.ReferenceId}' cannot be embedded because it was not found in components.");
                        }
                        if (value is not OpenApiResponse example)
                        {
                            throw new InvalidOperationException($"Response reference '{oaRRa.ReferenceId}' cannot be embedded because it is not an OpenApiResponse.");
                        }
                        response = example.Clone();
                    }
                    else
                    {
                        response = new OpenApiResponseReference(oaRRa.ReferenceId);
                    }
                    // Apply any description override
                    if (oaRRa.Description is not null)
                    {
                        response.Description = oaRRa.Description;
                    }
                    // Add to responses
                    openApiAttr.Responses.Add(oaRRa.StatusCode, response);
                }
                else if (attr is OpenApiResponseAttribute oaRa)
                {
                    // Create response inline
                    openApiAttr.Responses ??= [];
                    // Create a new response
                    var response = new OpenApiResponse();
                    // Populate from attribute
                    if (CreateResponseFromAttribute(oaRa, response))
                    {
                        openApiAttr.Responses.Add(oaRa.StatusCode, response);
                    }
                }
                else if (attr is OpenApiRequestBodyRefAttribute oaRBra)
                {
                    if (oaRBra.Inline)
                    {
                        if (Document.Components?.RequestBodies == null || !Document.Components.RequestBodies.TryGetValue(oaRBra.ReferenceId, out var requestBody))
                        {
                            throw new InvalidOperationException($"RequestBody reference '{oaRBra.ReferenceId}' cannot be embedded because it was not found in components.");
                        }
                        if (requestBody is not OpenApiRequestBody example)
                        {
                            throw new InvalidOperationException($"RequestBody reference '{oaRBra.ReferenceId}' cannot be embedded because it is not an OpenApiRequestBody.");
                        }
                        openApiAttr.RequestBody = example.Clone();
                    }
                    else
                    {
                        openApiAttr.RequestBody = new OpenApiRequestBodyReference(oaRBra.ReferenceId);
                    }
                    if (oaRBra.Description is not null)
                    {
                        openApiAttr.RequestBody.Description = oaRBra.Description;
                    }
                }
                else
                {
                    if (attr is KestrunAnnotation ka)
                    {
                        throw new InvalidOperationException(
                            $"Unhandled Kestrun annotation: {ka.GetType().Name}");
                    }
                }
            }
            // Process parameters for [OpenApiParameter] attributes
            foreach (var param in func.Parameters.Values)
            {
                // Check for [OpenApiParameter] attribute on the parameter
                var paramAttrs = param.Attributes;
                IOpenApiParameter? iparameter = null;
                foreach (var pAttr in paramAttrs)
                {
                    if (pAttr is OpenApiParameterAttribute oaParamAttr)
                    {
                        openApiAttr.Parameters ??= [];
                        var parameter = new OpenApiParameter();
                        iparameter = parameter;
                        if (CreateParameterFromAttribute(oaParamAttr, parameter))
                        {
                            if (string.IsNullOrEmpty(parameter.Name))
                            {
                                parameter.Name = param.Name;
                            }
                            parameter.Schema = InferPrimitiveSchema(param.ParameterType);
                            openApiAttr.Parameters.Add(parameter);
                        }
                    }
                    else if (pAttr is OpenApiParameterRefAttribute oaParamRefAttr)
                    {
                        openApiAttr.Parameters ??= [];
                        IOpenApiParameter parameter;
                        // Determine if we inline the referenced parameter or use a $ref
                        if (oaParamRefAttr.Inline)
                        {
                            if (Document.Components?.Parameters == null || !Document.Components.Parameters.TryGetValue(oaParamRefAttr.ReferenceId, out var parameterValue))
                            {
                                throw new InvalidOperationException($"Parameter reference '{oaParamRefAttr.ReferenceId}' cannot be embedded because it was not found in components.");
                            }
                            if (parameterValue is not OpenApiParameter valueClone)
                            {
                                throw new InvalidOperationException($"Parameter reference '{oaParamRefAttr.ReferenceId}' is not an OpenApiParameter and cannot be inlined.");
                            }
                            var parameterClone = valueClone.Clone();
                            // Apply any name override
                            if (!string.IsNullOrEmpty(oaParamRefAttr.Name))
                            {
                                parameterClone.Name = oaParamRefAttr.Name;
                            }
                            parameter = parameterClone;
                        }
                        else
                        {
                            parameter = new OpenApiParameterReference(oaParamRefAttr.ReferenceId);
                        }
                        iparameter = parameter;
                        openApiAttr.Parameters.Add(parameter);
                    }
                    else
                    {
                        if (pAttr is KestrunAnnotation ka)
                        {
                            throw new InvalidOperationException(
                                $"Unhandled Kestrun annotation: {ka.GetType().Name}");
                        }
                    }
                }
                if (iparameter is not null and OpenApiParameter pmtr)
                {
                    var pt = param.ParameterType;
                    /* var allowNull = false;
                     var underlying = Nullable.GetUnderlyingType(pt);
                     if (underlying != null)
                     {
                         allowNull = true;
                         pt = underlying;
                     }*/
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
                    // Apply any [OpenApiProperty] attribute on the parameter to the schema
                    pmtr.Schema = ps;
                }
            }

            routeOptions.OpenAPI.Add(parsedVerb, openApiAttr);
            // Default pattern if none provided: "/<FunctionName>"
            if (string.IsNullOrWhiteSpace(routeOptions.Pattern))
            {
                routeOptions.Pattern = "/" + func.Name;
            }

            // Script source
            routeOptions.ScriptCode.ScriptBlock = sb;

            // Register the route
            _ = Host.AddMapRoute(routeOptions);
        }
    }

    /// <summary>
    /// Applies a security scheme to the OpenAPI document based on the provided authentication options.
    /// </summary>
    /// <param name="scheme">The name of the security scheme.</param>
    /// <param name="options">The authentication options.</param>
    /// <exception cref="NotSupportedException">Thrown when the authentication options type is not supported.</exception>
    internal void ApplySecurityScheme(string scheme, IOpenApiAuthenticationOptions options)
    {
        Document.Components ??= new OpenApiComponents();
        var securityScheme = options switch
        {
            ApiKeyAuthenticationOptions apiKeyOptions => GetSecurityScheme(apiKeyOptions),
            BasicAuthenticationOptions basicOptions => GetSecurityScheme(basicOptions),
            CookieAuthOptions cookieOptions => GetSecurityScheme(cookieOptions),
            JwtAuthOptions jwtOptions => GetSecurityScheme(jwtOptions),
            OAuth2Options oauth2Options => GetSecurityScheme(oauth2Options),
            _ => throw new NotSupportedException($"Unsupported authentication options type: {options.GetType().FullName}"),
        };
        AddSecurityComponent(scheme: scheme, globalScheme: options.GlobalScheme, securityScheme: securityScheme);
    }

    /// <summary>
    /// Gets the OpenAPI security scheme for OAuth2 authentication.
    /// </summary>
    /// <param name="options">The OAuth2 authentication options.</param>
    /// <returns></returns>
    private static OpenApiSecurityScheme GetSecurityScheme(OAuth2Options options)
    {
        // Build OAuth flows
        var flows = new OpenApiOAuthFlows();
        // Client Credentials flow
        if (options.Flow == OAuthFlowType.ClientCredentials)
        {
            flows.ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri(options.TokenEndpoint, UriKind.Absolute),
                Scopes = options.Scope.ToDictionary(s => s, s => s)
            };
        }
        else // Authorization Code flow
        {
            flows.AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri(options.AuthorizationEndpoint, UriKind.Absolute),
                TokenUrl = new Uri(options.TokenEndpoint, UriKind.Absolute),
                Scopes = options.Scope.ToDictionary(s => s, s => s)
            };
        }
        return new OpenApiSecurityScheme()
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = flows,
            Description = options.Description
        };
    }
    /// <summary>
    /// Gets the OpenAPI security scheme for API key authentication.
    /// </summary>
    /// <param name="options">The API key authentication options.</param>
    private static OpenApiSecurityScheme GetSecurityScheme(ApiKeyAuthenticationOptions options)
    {
        return new OpenApiSecurityScheme()
        {
            Type = SecuritySchemeType.ApiKey,
            Name = options.ApiKeyName,
            In = options.In,
            Description = options.Description
        };
    }

    /// <summary>
    /// Gets the OpenAPI security scheme for cookie authentication.
    /// </summary>
    /// <param name="options">The cookie authentication options.</param>
    /// <returns></returns>
    private static OpenApiSecurityScheme GetSecurityScheme(CookieAuthOptions options)
    {
        return new OpenApiSecurityScheme()
        {
            Type = SecuritySchemeType.ApiKey,
            Name = options.Cookie.Name,
            In = ParameterLocation.Cookie,
            Description = options.Description
        };
    }

    /// <summary>
    /// Gets the OpenAPI security scheme for JWT authentication.
    /// </summary>
    /// <param name="options">The JWT authentication options.</param>
    /// <returns></returns>
    private static OpenApiSecurityScheme GetSecurityScheme(JwtAuthOptions options)
    {
        return new OpenApiSecurityScheme()
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = options.Description
        };
    }

    /// <summary>
    ///  Gets the OpenAPI security scheme for basic authentication.
    /// </summary>
    /// <param name="options">The basic authentication options.</param>
    private static OpenApiSecurityScheme GetSecurityScheme(BasicAuthenticationOptions options)
    {
        return new OpenApiSecurityScheme()
        {
            Type = SecuritySchemeType.Http,
            Scheme = "basic",
            Description = options.Description
        };
    }

    /// <summary>
    /// Adds a security component to the OpenAPI document.
    /// </summary>
    /// <param name="scheme">The name of the security component.</param>
    /// <param name="globalScheme">Indicates whether the security scheme should be applied globally.</param>
    /// <param name="securityScheme">The security scheme to add.</param>
    private void AddSecurityComponent(string scheme, bool globalScheme, OpenApiSecurityScheme securityScheme)
    {
        _ = Document.AddComponent(scheme, securityScheme);

        // Reference it by NAME in the requirement (no .Reference in v2)
        var requirement = new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference(scheme,Document), new List<string>()
            }
        };
        SecurityRequirement.Add(scheme, requirement);

        // Apply globally if specified
        if (globalScheme)
        {
            // Apply globally
            Document.Security ??= [];
            Document.Security.Add(requirement);
        }
    }

}
