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
    /// Default documentation identifier.
    /// </summary>
    public const string DefaultDocumentationId = "Default";

    /// <summary>
    /// Default documentation identifiers for OpenAPI authentication schemes.
    /// </summary>
    public static readonly string[] DefaultDocumentationIds = ["Default"];
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
    public Dictionary<(string Pattern, HttpVerb Method), OpenAPIPathMetadata> WebHook { get; set; } = [];

    /// <summary>
    /// OpenAPI metadata for callbacks associated with this document.
    /// </summary>
    public Dictionary<(string Pattern, HttpVerb Method), OpenAPIPathMetadata> Callbacks { get; set; } = [];

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
#if EXTENDED_OPENAPI
        ProcessComponentTypes(components.ParameterTypes, () => Document.Components.Parameters ??= new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal), BuildParameters);

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
        // Auto-discover OpenAPI component types
        var components = OpenApiSchemaDiscovery.GetOpenApiTypesAuto();

        // Generate components from the discovered types
        GenerateComponents(components);

        // Process variable annotations from the host
        ProcessVariableAnnotations(Host.ComponentAnnotations);
    }

    /// <summary>
    /// Processes variable annotations to build OpenAPI components.
    /// </summary>
    /// <param name="annotations">A dictionary of variable names to their annotated variables.</param>
    private void ProcessVariableAnnotations(Dictionary<string, OpenApiComponentAnnotationScanner.AnnotatedVariable>? annotations)
    {
        if (annotations is null || annotations.Count == 0)
        {
            Host.Logger.Warning("No OpenAPI component annotations were found in the host.");
            return;
        }
        foreach (var (variableName, variable) in annotations)
        {
            if (variable?.Annotations is null || variable.Annotations.Count == 0)
            {
                continue;
            }

            DispatchComponentAnnotations(variableName, variable);
        }
    }

    /// <summary>
    /// Dispatches component annotations for a given variable.
    /// </summary>
    /// <param name="variableName">The name of the variable.</param>
    /// <param name="variable">The annotated variable containing annotations.</param>
    private void DispatchComponentAnnotations(string variableName, OpenApiComponentAnnotationScanner.AnnotatedVariable variable)
    {
        foreach (var annotation in variable.Annotations)
        {
            switch (annotation)
            {
                case OpenApiParameterComponent paramComponent:
                    ProcessParameterComponent(variableName, variable, paramComponent);
                    break;

                case OpenApiParameterExampleRefAttribute exampleRef:
                    ProcessParameterExampleRef(variableName, exampleRef);
                    break;

                // future:
                // case OpenApiHeaderComponent header:
                //     ProcessHeaderComponent(variableName, variable, header);
                //     break;

                default:
                    break;
            }
        }
    }

    public void GenerateComponents2()
    {
        var components = OpenApiSchemaDiscovery.GetOpenApiTypesAuto();
        GenerateComponents(components);
        var annotations = Host.ComponentAnnotations;
        if (annotations is null)
        {
            Host.Logger.Warning("No OpenAPI component annotations were found in the host.");
            return;
        }

        foreach (var (variableName, variable) in annotations)
        {
            foreach (var annotation in variable.Annotations)
            {
                if (annotation is OpenApiParameterComponent parameterAnnotation)
                {
                    // Process OpenApiParameterComponent annotations
                    // foreach (var annotation in variable.Annotations
                    var parameter = GetOrCreateParameterItem(variableName, parameterAnnotation.Inline);

                    parameter.AllowEmptyValue = parameterAnnotation.AllowEmptyValue;
                    parameter.Description = parameterAnnotation.Description;
                    parameter.In = parameterAnnotation.In.ToOpenApi();
                    parameter.Name = parameterAnnotation.Name ?? variableName;
                    parameter.Style = parameterAnnotation.Style?.ToOpenApi();
                    parameter.AllowReserved = parameterAnnotation.AllowReserved;
                    parameter.Required = parameterAnnotation.Required;
                    parameter.Example = OpenApiJsonNodeFactory.FromObject(parameterAnnotation.Example);
                    parameter.Deprecated = parameterAnnotation.Deprecated;
                    // Explode defaults to true for "form" and "cookie" styles
                    if (parameterAnnotation.Explode || (parameter.Style is ParameterStyle.Form or ParameterStyle.Cookie))
                    {
                        parameter.Explode = true;
                    }

                    if (variable.VariableType != null)
                    {
                        if (InferPrimitiveSchema(variable.VariableType) is OpenApiSchema schema)
                        {
                            ApplyConcreteSchemaAttributes(parameterAnnotation, schema);

                            if (variable.InitialValue != null)
                            {
                                schema.Default = OpenApiJsonNodeFactory.FromObject(variable.InitialValue);
                            }
                            if (string.IsNullOrWhiteSpace(parameterAnnotation.ContentType))
                            {
                                parameter.Schema = schema;
                            }
                            else
                            {
                                parameter.Content ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);
                                parameter.Content[parameterAnnotation.ContentType] = new OpenApiMediaType
                                {
                                    Schema = schema
                                };
                            }
                        }
                    }
                }
                if (annotation is OpenApiParameterExampleRefAttribute exampleRefAttribute)
                {
                    // Process OpenApiCallbackRefAttribute annotations
                    var parameter = GetOrCreateParameterItem(variableName, false);

                    _ = TryAddExample(parameter.Examples ??= new Dictionary<string, IOpenApiExample>(), exampleRefAttribute);
                }
            }
        }
    }

    /// <summary>
    /// Generates the OpenAPI document by processing components and building paths and webhooks.
    /// </summary>
    /// <remarks>BuildCallbacks is already handled elsewhere.</remarks>
    /// <remarks>This method sets HasBeenGenerated to true after generation.</remarks>
    public void GenerateDoc()
    {
        // First, generate components
        GenerateComponents();

        // Then, generate webhooks
        BuildWebhooks(WebHook);

        // Finally, build paths from registered routes
        BuildPathsFromRegisteredRoutes(Host.RegisteredRoutes);

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
