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
using Kestrun.Authentication;
using Kestrun.Runtime;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
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

        ProcessComponentTypes(components.ExampleTypes, () => Document.Components.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal), BuildExamples);
        ProcessComponentTypes(components.SchemaTypes, () => Document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal), t => BuildSchema(t));
        ProcessComponentTypes(components.HeaderTypes, () => Document.Components.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal), BuildHeaders);
        ProcessComponentTypes(components.ParameterTypes, () => Document.Components.Parameters ??= new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal), BuildParameters);
        ProcessComponentTypes(components.LinkTypes, () => Document.Components.Links ??= new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal), BuildLinks);
        ProcessComponentTypes(components.CallbackTypes, () => Document.Components.Callbacks ??= new Dictionary<string, IOpenApiCallback>(StringComparer.Ordinal), BuildCallbacks);
        ProcessComponentTypes(components.ResponseTypes, () => Document.Components.Responses ??= new Dictionary<string, IOpenApiResponse>(StringComparer.Ordinal), BuildResponses);
        ProcessComponentTypes(components.RequestBodyTypes, () => Document.Components.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal), BuildRequestBodies);
    }

    /// <summary>
    /// Processes a list of component types and builds them into the OpenAPI document.
    /// </summary>
    /// <param name="types">The list of component types to process.</param>
    /// <param name="ensureDictionary">An action to ensure the corresponding dictionary is initialized.</param>
    /// <param name="buildAction">An action to build each component type.</param>
    private static void ProcessComponentTypes(
        IReadOnlyList<Type>? types,
        Action ensureDictionary,
        Action<Type> buildAction)
    {
        if (types is null || types.Count == 0)
        {
            return;
        }

        ensureDictionary();
        foreach (var type in types)
        {
            buildAction(type);
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
                if (!map.OpenAPI.TryGetValue(method, out var meta) || (!meta.Enabled))
                {
                    // Skip silent routes by default
                    continue;
                }

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
        if (meta.Tags.Count > 0)
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
        if (meta.SecuritySchemes is not null && meta.SecuritySchemes.Count != 0)
        {
            op.Security ??= [];

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var schemeName in meta.SecuritySchemes
                         .SelectMany(d => d.Keys)
                         .Distinct())
            {
                if (!seen.Add(schemeName))
                {
                    continue;
                }
                // Gather scopes for this scheme
                var scopesForScheme = meta.SecuritySchemes
                    .SelectMany(dict => dict)
                    .Where(kv => kv.Key == schemeName)
                    .SelectMany(kv => kv.Value)
                    .Distinct()
                    .ToList();
                // Build requirement
                var requirement = new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(schemeName, Document)] = scopesForScheme
                };

                op.Security.Add(requirement);
            }
        }
        else if (meta.SecuritySchemes is not null && meta.SecuritySchemes.Count == 0)
        {
            // Explicitly anonymous for this operation (overrides Document.Security)
            op.Security = [];
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
                foreach (var schemaComp in t.GetCustomAttributes(typeof(OpenApiPropertyAttribute)))
                {
                    if (schemaComp is OpenApiPropertyAttribute prop)
                    {
                        if (prop.Array)
                        {
                            var s = new OpenApiSchema
                            {
                                Type = JsonSchemaType.Array,
                                Items = new OpenApiSchemaReference(t.BaseType.Name),
                            };
                            ApplySchemaAttr(prop, s);
                            return s;
                        }
                        else
                        {
                            var s = new OpenApiSchemaReference(t.BaseType.Name); // Ensure base type schema is built first
                            ApplySchemaAttr(prop, s);
                            return s;
                        }
                    }
                }

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

        foreach (var a in t.GetCustomAttributes(true)
          .Where(a => a is OpenApiPropertyAttribute or OpenApiSchemaComponent))
        {
            ApplySchemaAttr(a as OpenApiProperties, schema);
            /*    if (a is OpenApiPropertyAttribute propAttribute)
                {
                    // Apply property-level attributes to the schema
                    ApplySchemaAttr(propAttribute, schema);
                }
                else if (a is OpenApiSchemaComponent schemaAttribute)
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
                    //  schema.AdditionalPropertiesAllowed |= schemaAttribute.AdditionalPropertiesAllowed;
                    // Apply additionalProperties schema if specified
                    ApplyAdditionalProperties(schema, schemaAttribute);
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

                    if (schemaAttribute.Required is not null && schemaAttribute.Required.Length > 0)
                    {
                        schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
                        foreach (var r in schemaAttribute.Required)
                        {
                            _ = schema.Required.Add(r);
                        }
                    }
                }*/
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
    /// <param name="requestBodyPreferred"> Indicates if the schema is for a request body (affects certain type inferences).</param>
    /// <param name="inline">Indicates if the schema should be inlined.</param>
    /// <returns>The inferred OpenApiSchema.</returns>
    private IOpenApiSchema InferPrimitiveSchema(Type type, bool requestBodyPreferred = false, bool inline = false)
    {
        // Direct type mappings
        if (PrimitiveSchemaMap.TryGetValue(type, out var schemaFactory))
        {
            return schemaFactory();
        }

        // Group checks for integer types
        if (IsInt32Type(type))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" };
        }

        if (IsNumericType(type))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Number };
        }

        if (IsUnsignedIntegerType(type))
        {
            return new OpenApiSchema { Type = JsonSchemaType.Integer };
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
                if (requestBodyPreferred)
                {
                    if (GetRequestBody(type.Name) is OpenApiRequestBody rb)
                    {
                        return rb.ConvertToSchema();
                    }
                    if (GetSchema(type.Name) is OpenApiSchema schema)
                    {
                        return schema.Clone();
                    }
                }
                else
                {
                    if (GetSchema(type.Name) is OpenApiSchema schema)
                    {
                        return schema.Clone();
                    }
                    if (GetRequestBody(type.Name) is OpenApiRequestBody rb)
                    {
                        return rb.ConvertToSchema();
                    }
                }
            }
            else
            {
                if (requestBodyPreferred)
                {
                    if (GetRequestBody(type.Name) is OpenApiRequestBody rb)
                    {
                        return new OpenApiSchemaReference(type.Name);
                    }
                    if (GetSchema(type.Name) is OpenApiSchema schema)
                    {
                        return new OpenApiSchemaReference(type.Name);
                    }
                }
                else
                {
                    if (GetSchema(type.Name) is OpenApiSchema schema)
                    {
                        return new OpenApiSchemaReference(type.Name);
                    }
                    if (GetRequestBody(type.Name) is OpenApiRequestBody rb)
                    {
                        return new OpenApiSchemaReference(type.Name);
                    }
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
        [typeof(char)] = () => new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 1, MinLength = 1 }
    };

    /// <summary>
    /// Determines if the type is a 32-bit integer type (int, short, byte).
    /// </summary>
    /// <param name="t"> The type to check.</param>
    /// <returns>True if the type is a 32-bit integer type; otherwise, false.</returns>
    private static bool IsInt32Type(Type t) => t == typeof(int) || t == typeof(short) || t == typeof(byte);

    /// <summary>
    /// Determines if the type is a numeric type (float, double, decimal).
    /// </summary>
    /// <param name="t">The type to check.</param>
    /// <returns>True if the type is a numeric type; otherwise, false.</returns>
    private static bool IsNumericType(Type t) => t == typeof(float) || t == typeof(double) || t == typeof(decimal);

    /// <summary>
    /// Determines if the type is an unsigned integer type (sbyte, ushort, uint, ulong).
    /// </summary>
    /// <param name="t">The type to check.</param>
    /// <returns>True if the type is an unsigned integer type; otherwise, false.</returns>
    private static bool IsUnsignedIntegerType(Type t) => t == typeof(sbyte) || t == typeof(ushort) || t == typeof(uint) || t == typeof(ulong);

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
        => t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(Guid) || t == typeof(object);
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
                         .Cast<KestrunAnnotation>()
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
    //todo: unify with BuildPropertySchema with BuildSchemaForType
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
            PowerShellAttributes.ApplyPowerShellValidationAttributes(p, s);
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
            PowerShellAttributes.ApplyPowerShellValidationAttributes(p, s);
            // If no explicit default provided via schema attribute, try to pull default from property value
            if (s is OpenApiSchema sc && sc.Default is null)
            {
                try
                {
                    var inst = Activator.CreateInstance(t);
                    var val = p.GetValue(inst);
                    if (!IsIntrinsicDefault(val, p.PropertyType))
                    {
                        sc.Default = ToNode(val);
                    }
                }
                catch { }

                if (allowNull)
                {
                    sc.Type |= JsonSchemaType.Null;
                }
            }
            paramSchema = s;
        }
        return paramSchema;
    }

    private bool CreateParameterFromAttribute(KestrunAnnotation attr, OpenApiParameter parameter)
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
                PowerShellAttributes.ApplyPowerShellValidationAttributes(p, schema);
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
                PowerShellAttributes.ApplyPowerShellValidationAttributes(p, schema);
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
                var sc = InferPrimitiveSchema(pt);
                if (sc is OpenApiSchema schema)
                {
                    ApplySchemaAttr(p.GetCustomAttribute<OpenApiPropertyAttribute>(), schema);
                    PowerShellAttributes.ApplyPowerShellValidationAttributes(p, schema);
                    if (allowNull)
                    {
                        schema.Type |= JsonSchemaType.Null;
                    }
                    iSchema = schema;
                }
                else
                {
                    iSchema = sc;
                }
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

        return attr switch
        {
            OpenApiResponseAttribute resp => ApplyResponseAttribute(resp, response),
            OpenApiHeaderRefAttribute href => ApplyHeaderRefAttribute(href, response),
            OpenApiLinkRefAttribute lref => ApplyLinkRefAttribute(lref, response),
            OpenApiExampleRefAttribute exRef => ApplyExampleRefAttribute(exRef, response),
            _ => false
        };
    }
    // --- local helpers -------------------------------------------------------


    private bool ApplyResponseAttribute(OpenApiResponseAttribute resp, OpenApiResponse response)
    {
        if (!string.IsNullOrEmpty(resp.Description))
        {
            response.Description = resp.Description;
        }

        // Decide which schema to use
        IOpenApiSchema? schema = null;
        // 1) Type-based schema (new behavior)
        if (resp.Schema is not null)
        {
            // For responses, requestBodyPreferred = false so we prefer component schema over requestBody
            schema = InferPrimitiveSchema(resp.Schema, requestBodyPreferred: false, inline: resp.Inline);
        }
        // 2) Component reference (existing behavior)
        else if (resp.SchemaRef is not null)
        {
            schema = resp.Inline
                ? CloneSchemaOrThrow(resp.SchemaRef)
                : new OpenApiSchemaReference(resp.SchemaRef);
        }

        // If we have a schema, apply it to all declared content types
        if (schema is not null && resp.ContentType is { Length: > 0 })
        {
            foreach (var ct in resp.ContentType)
            {
                var media = GetOrAddMediaType(response, ct);
                media.Schema = schema; // or schema.Clone() if you need per-media isolation
            }
        }

        return true;
        /*    if (resp.SchemaRef is not null)
            {
                foreach (var ct in resp.ContentTypes)
                {
                    var media = GetOrAddMediaType(response, ct);
                    media.Schema = resp.Inline
                        ? CloneSchemaOrThrow(resp.SchemaRef)
                        : new OpenApiSchemaReference(resp.SchemaRef);
                }
            }
            return true;*/
    }

    private bool ApplyHeaderRefAttribute(OpenApiHeaderRefAttribute href, OpenApiResponse response)
    {
        (response.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal))[href.Key] = new OpenApiHeaderReference(href.ReferenceId);
        return true;
    }

    private bool ApplyLinkRefAttribute(OpenApiLinkRefAttribute lref, OpenApiResponse response)
    {
        (response.Links ??= new Dictionary<string, IOpenApiLink>(StringComparer.Ordinal))[lref.Key] = new OpenApiLinkReference(lref.ReferenceId);
        return true;
    }

    private bool ApplyExampleRefAttribute(OpenApiExampleRefAttribute exRef, OpenApiResponse response)
    {
        var targets = exRef.ContentType is null
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
    /// Creates an OpenApiHeader from the specified supported attribute types.
    /// </summary>
    /// <param name="attr">Attribute instance.</param>
    /// <param name="header">Target header to populate.</param>
    /// <returns>True when the attribute type was recognized and applied; otherwise false.</returns>
    private static bool CreateHeaderFromAttribute(object attr, OpenApiHeader header)
    {
        return attr switch
        {
            OpenApiHeaderAttribute h => ApplyHeaderAttribute(h, header),
            OpenApiExampleRefAttribute exRef => ApplyExampleRefAttribute(exRef, header),
            OpenApiExampleAttribute ex => ApplyInlineExampleAttribute(ex, header),
            _ => false
        };
    }

    private static bool ApplyHeaderAttribute(OpenApiHeaderAttribute attribute, OpenApiHeader header)
    {
        header.Description = attribute.Description;
        header.Required = attribute.Required;
        header.Deprecated = attribute.Deprecated;
        header.AllowEmptyValue = attribute.AllowEmptyValue;
        header.Schema = string.IsNullOrWhiteSpace(attribute.SchemaRef)
            ? new OpenApiSchema { Type = JsonSchemaType.String }
            : new OpenApiSchemaReference(attribute.SchemaRef);
        header.Style = attribute.Style.ToOpenApi();
        header.AllowReserved = attribute.AllowReserved;
        header.Explode = attribute.Explode;
        if (attribute.Example is not null)
        {
            header.Example = ToNode(attribute.Example);
        }
        return true;
    }

    private static bool ApplyExampleRefAttribute(OpenApiExampleRefAttribute exRef, OpenApiHeader header)
    {
        header.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        if (header.Examples.ContainsKey(exRef.Key))
        {
            throw new InvalidOperationException($"Header already contains an example with the key '{exRef.Key}'.");
        }
        header.Examples[exRef.Key] = new OpenApiExampleReference(exRef.ReferenceId);
        return true;
    }

    private static bool ApplyInlineExampleAttribute(OpenApiExampleAttribute ex, OpenApiHeader header)
    {
        if (ex.Key is null)
        {
            throw new InvalidOperationException("OpenApiExampleAttribute requires a non-null Name property.");
        }
        header.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        if (header.Examples.ContainsKey(ex.Key))
        {
            throw new InvalidOperationException($"Header already contains an example with the key '{ex.Key}'.");
        }
        header.Examples[ex.Key] = new OpenApiExample
        {
            Summary = ex.Summary,
            Description = ex.Description,
            Value = ToNode(ex.Value),
            ExternalValue = ex.ExternalValue
        };
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
        var componentSchema = BuildSchemaForType(t);
        var requestBody = new OpenApiRequestBody();

        var classAttrs = t.GetCustomAttributes(inherit: false)
                          .Where(a => a is OpenApiRequestBodyComponent or OpenApiExampleRefAttribute or OpenApiPropertyAttribute)
                          .OrderBy(a => a is not OpenApiRequestBodyComponent)
                          .ToArray();

        var name = string.Empty;
        foreach (var attr in classAttrs)
        {
            try
            {
                _ = attr switch
                {
                    OpenApiRequestBodyComponent body => ApplyRequestBodyComponent(body, ref name, requestBody, componentSchema),
                    OpenApiPropertyAttribute prop => ApplyRequestBodySchemaProperty(prop, requestBody, ref componentSchema),
                    OpenApiExampleRefAttribute ex => ApplyRequestBodyExampleRef(ex, requestBody),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error applying attribute '{attr.GetType().Name}' to request body of type '{t.FullName}': {ex.Message}", ex);
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = t.Name; // fallback to type name if no explicit key was provided
        }
        Document.Components!.RequestBodies![name] = requestBody;
    }

    // --- RequestBody helpers (extracted to reduce cyclomatic complexity) ---------
    private bool ApplyRequestBodyComponent(OpenApiRequestBodyComponent bodyAttribute, ref string name, OpenApiRequestBody requestBody, IOpenApiSchema schema)
    {
        var explicitKey = GetKeyOverride(bodyAttribute);
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            name = explicitKey;
        }

        if (bodyAttribute.Description is not null)
        {
            requestBody.Description = bodyAttribute.Description;
        }
        requestBody.Required |= bodyAttribute.Required;
        requestBody.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal);

        var mediaType = new OpenApiMediaType { Schema = schema };
        if (bodyAttribute.Example is not null)
        {
            mediaType.Example = ToNode(bodyAttribute.Example);
        }

        foreach (var ct in bodyAttribute.ContentType)
        {
            requestBody.Content[ct] = mediaType;
        }
        return true;
    }

    private bool ApplyRequestBodySchemaProperty(OpenApiPropertyAttribute schemaAttr, OpenApiRequestBody requestBody, ref IOpenApiSchema schema)
    {
        if (schemaAttr.Array && schema is OpenApiSchemaReference)
        {
            // Wrap referenced component schema in array representation
            schema = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = schema
            };
        }
        ApplySchemaAttr(schemaAttr, schema);

        requestBody.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
        {
            ["application/json"] = new OpenApiMediaType()
        };
        foreach (var mt in requestBody.Content.Values)
        {
            mt.Schema = schema;
        }
        return true;
    }

    private bool ApplyRequestBodyExampleRef(OpenApiExampleRefAttribute exRef, OpenApiRequestBody requestBody)
    {
        requestBody.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal);
        var targets = ResolveExampleContentTypes(exRef, requestBody);
        foreach (var ct in targets)
        {
            var mediaType = requestBody.Content.TryGetValue(ct, out var existing)
                ? existing
                : (requestBody.Content[ct] = new OpenApiMediaType());

            mediaType.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
            mediaType.Examples[exRef.Key] = exRef.Inline
                ? CloneExampleOrThrow(exRef.ReferenceId)
                : new OpenApiExampleReference(exRef.ReferenceId);
        }
        return true;
    }

    private IEnumerable<string> ResolveExampleContentTypes(OpenApiExampleRefAttribute exRef, OpenApiRequestBody requestBody)
    {
        var keys = exRef.ContentType is null ? (requestBody.Content?.Keys ?? Array.Empty<string>()) : [exRef.ContentType];
        return keys.Count == 0 ? ["application/json"] : (IEnumerable<string>)keys;
    }

    private IOpenApiExample CloneExampleOrThrow(string referenceId)
    {
        return Document.Components?.Examples == null || !Document.Components.Examples.TryGetValue(referenceId, out var value)
            ? throw new InvalidOperationException($"Example reference '{referenceId}' cannot be embedded because it was not found in components.")
            : value is not OpenApiExample example
            ? throw new InvalidOperationException($"Example reference '{referenceId}' cannot be embedded because it is not an OpenApiExample.")
            : (IOpenApiExample)example.Clone();
    }

    #endregion

    /// <summary>
    /// Builds an inline schema representation from the specified type.
    /// </summary>
    /// <param name="t">The type to build the schema from.</param>
    /// <returns>An inline schema representation of the specified type.</returns>
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

    private static bool CreateRequestBodyFromAttribute(KestrunAnnotation attribute, OpenApiRequestBody requestBody, IOpenApiSchema schema)
    {
        switch (attribute)
        {
            case OpenApiRequestBodyAttribute request:
                requestBody.Description = request.Description;
                requestBody.Required = request.Required;
                // Content
                requestBody.Content ??= new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal);
                var mediaType = new OpenApiMediaType();
                // Example
                if (request.Example is not null)
                {
                    mediaType.Example = ToNode(request.Example);
                }
                // Schema
                mediaType.Schema = schema;
                foreach (var contentType in request.ContentType)
                {
                    requestBody.Content[contentType] = mediaType;
                }
                return true;
            default:
                return false;
        }
    }

    private OpenApiSchema GetSchema(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // Look up schema in components
        return Document.Components?.Schemas is { } schemas
               && schemas.TryGetValue(id, out var p)
               && p is OpenApiSchema op
            ? op
            : throw new InvalidOperationException($"Schema '{id}' not found.");
    }

    private OpenApiParameter GetParameter(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // Look up parameter in components
        return Document.Components?.Parameters is { } parameters
               && parameters.TryGetValue(id, out var p)
               && p is OpenApiParameter op
            ? op
            : throw new InvalidOperationException($"Parameter '{id}' not found.");
    }

    private OpenApiRequestBody GetRequestBody(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // Look up request body in components
        return Document.Components?.RequestBodies is { } requestBodies
               && requestBodies.TryGetValue(id, out var p)
               && p is OpenApiRequestBody op
            ? op
            : throw new InvalidOperationException($"RequestBody '{id}' not found.");
    }
    /*
        private OpenApiRequestBody? GetRequestBody(string id)
        {
            if (Document.Components?.RequestBodies is null)
            {
                return null;
            }
            if (Document.Components.RequestBodies.ContainsKey(id))
            {
                var r = Document.Components?.RequestBodies[id];
                if (r is OpenApiRequestBody obr)
                {
                    return obr;
                }

            }
            return null;
        }*/

    private OpenApiHeader GetHeader(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // Look up header in components
        return Document.Components?.Headers is { } headers
               && headers.TryGetValue(id, out var p)
               && p is OpenApiHeader op
            ? op
            : throw new InvalidOperationException($"Header '{id}' not found.");
    }

    private OpenApiResponse GetResponse(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // Look up response in components
        return Document.Components?.Responses is { } responses
               && responses.TryGetValue(id, out var p)
               && p is OpenApiResponse op
            ? op
            : throw new InvalidOperationException($"Response '{id}' not found.");
    }

    private bool ComponentSchemasExists(string id) =>
        Document.Components?.Schemas?.ContainsKey(id) == true;

    private bool ComponentRequestBodiesExists(string id) =>
        Document.Components?.RequestBodies?.ContainsKey(id) == true;

    private bool ComponentResponsesExists(string id) =>
        Document.Components?.Responses?.ContainsKey(id) == true;
    private bool ComponentParametersExists(string id) =>
        Document.Components?.Parameters?.ContainsKey(id) == true;

    private bool ComponentExamplesExists(string id) =>
            Document.Components?.Examples?.ContainsKey(id) == true;

    private bool ComponentHeadersExists(string id) =>
            Document.Components?.Headers?.ContainsKey(id) == true;
    private bool ComponentCallbacksExists(string id) =>
            Document.Components?.Callbacks?.ContainsKey(id) == true;

    private bool ComponentLinksExists(string id) =>
            Document.Components?.Links?.ContainsKey(id) == true;
    private bool ComponentPathItemsExists(string id) =>
            Document.Components?.PathItems?.ContainsKey(id) == true;

    /// <summary>
    /// Applies a security scheme to the OpenAPI document based on the provided authentication options.
    /// </summary>
    /// <param name="scheme">The name of the security scheme.</param>
    /// <param name="options">The authentication options.</param>
    /// <exception cref="NotSupportedException">Thrown when the authentication options type is not supported.</exception>
    public void ApplySecurityScheme(string scheme, IOpenApiAuthenticationOptions options)
    {
        var securityScheme = options switch
        {
            ApiKeyAuthenticationOptions apiKeyOptions => GetSecurityScheme(apiKeyOptions),
            BasicAuthenticationOptions basicOptions => GetSecurityScheme(basicOptions),
            CookieAuthOptions cookieOptions => GetSecurityScheme(cookieOptions),
            JwtAuthOptions jwtOptions => GetSecurityScheme(jwtOptions),
            OAuth2Options oauth2Options => GetSecurityScheme(oauth2Options),
            OidcOptions oidcOptions => GetSecurityScheme(oidcOptions),
            WindowsAuthOptions windowsOptions => GetSecurityScheme(windowsOptions),
            _ => throw new NotSupportedException($"Unsupported authentication options type: {options.GetType().FullName}"),
        };
        AddSecurityComponent(scheme: scheme, globalScheme: options.GlobalScheme, securityScheme: securityScheme);
    }

    /// <summary>
    /// Gets the OpenAPI security scheme for Windows authentication.
    /// </summary>
    /// <param name="options">The Windows authentication options.</param>
    /// <returns>The OpenAPI security scheme for Windows authentication.</returns>
    private static OpenApiSecurityScheme GetSecurityScheme(WindowsAuthOptions options)
    {
        return new OpenApiSecurityScheme()
        {
            Type = SecuritySchemeType.Http,
            Scheme = options.Protocol == WindowsAuthProtocol.Ntlm ? "ntlm" : "negotiate",
            Description = options.Description
        };
    }

    /// <summary>
    /// Gets the OpenAPI security scheme for OIDC authentication.
    /// </summary>
    /// <param name="options">The OIDC authentication options.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown when neither Authority nor MetadataAddress is set.</exception>
    private static OpenApiSecurityScheme GetSecurityScheme(OidcOptions options)
    {
        // Prefer explicit MetadataAddress if set
        var discoveryUrl = options.MetadataAddress
                           ?? (options.Authority is null
                               ? throw new InvalidOperationException(
                                   "Either Authority or MetadataAddress must be set to build OIDC OpenAPI scheme.")
                               : $"{options.Authority.TrimEnd('/')}/.well-known/openid-configuration");

        return new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OpenIdConnect,
            OpenIdConnectUrl = new Uri(discoveryUrl, UriKind.Absolute),
            // Description comes from AuthenticationSchemeOptions base class
            Description = options.Description
        };
    }

    /// <summary>
    /// Gets the OpenAPI security scheme for OAuth2 authentication.
    /// </summary>
    /// <param name="options">The OAuth2 authentication options.</param>
    /// <returns></returns>
    private static OpenApiSecurityScheme GetSecurityScheme(OAuth2Options options)
    {
        // Build OAuth flows
        var flows = new OpenApiOAuthFlows
        {
            // Client Credentials flow
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri(options.AuthorizationEndpoint, UriKind.Absolute),
            }
        };
        // Scopes
        if (options.ClaimPolicy is not null && options.ClaimPolicy.Policies is not null && options.ClaimPolicy.Policies.Count > 0)
        {
            var scopes = new Dictionary<string, string>();
            var policies = options.ClaimPolicy.Policies;
            foreach (var item in policies)
            {
                scopes.Add(item.Key, item.Value.Description ?? string.Empty);
            }
            flows.AuthorizationCode.Scopes = scopes;
        }
        // Token endpoint
        if (options.TokenEndpoint is not null)
        {
            flows.AuthorizationCode.TokenUrl = new Uri(options.TokenEndpoint, UriKind.Absolute);
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
