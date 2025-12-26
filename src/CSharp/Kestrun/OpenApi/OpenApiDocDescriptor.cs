using Microsoft.OpenApi;
using Kestrun.Hosting;
using Microsoft.OpenApi.Reader;
using System.Text;
using Kestrun.Hosting.Options;
using Kestrun.Utilities;

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
    /// Inline components specific to this OpenAPI document.
    /// </summary>
    public OpenApiComponents InlineComponents { get; }

    /// <summary>
    /// OpenAPI metadata for webhooks associated with this document.
    /// </summary>
    public Dictionary<(string Pattern, HttpVerb Method), OpenAPIMetadata> WebHook { get; set; } = []; // OpenAPI metadata for this route

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
        HasBeenGenerated = false;
        InlineComponents = new OpenApiComponents();
    }

    /// <summary>
    /// Indicates whether the OpenAPI document has been generated at least once.
    /// </summary>
    public bool HasBeenGenerated { get; private set; }
    /// <summary>
    /// Generates an OpenAPI document from the provided schema types.
    /// </summary>
    /// <param name="components">The set of discovered OpenAPI component types.</param>
    /// <returns>The generated OpenAPI document.</returns>
    internal void GenerateComponents(OpenApiComponentSet components)
    {
        Document.Components ??= new OpenApiComponents();
        ProcessComponentTypes(components.SchemaTypes, () => Document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal), t => BuildSchema(t));
        ProcessComponentTypes(components.ParameterTypes, () => Document.Components.Parameters ??= new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal), BuildParameters);
#if EXTENDED_OPENAPI
        ProcessComponentTypes(components.CallbackTypes, () => Document.Components.Callbacks ??= new Dictionary<string, IOpenApiCallback>(StringComparer.Ordinal), BuildCallbacks);
#endif
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
        BuildWebhooks(Host.OpenApiDocumentDescriptor[Authentication.IOpenApiAuthenticationOptions.DefaultSchemeName].WebHook);
        HasBeenGenerated = true;
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
}
