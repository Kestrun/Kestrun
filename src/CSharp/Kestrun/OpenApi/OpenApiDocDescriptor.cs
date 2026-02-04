using Microsoft.OpenApi;
using Kestrun.Hosting;
using Microsoft.OpenApi.Reader;
using System.Text;
using Kestrun.Hosting.Options;
using Kestrun.Utilities;
using System.Collections;
using System.Text.Json.Nodes;
using Kestrun.Forms;
using System.Reflection;

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

        AddFormOptions(components);

        // Process variable annotations from the host
        ProcessVariableAnnotations(Host.ComponentAnnotations);
    }

    private void AddFormOptions(OpenApiComponentSet components)
    {
        foreach (var type in components.SchemaTypes)
        {
            if (type is null || !type.IsDefined(typeof(KrBindFormAttribute), inherit: false))
            {
                continue;
            }

            var formOptions = BuildFormOptionsSchema(type.FullName, type);
            if (formOptions is null)
            {
                continue;
            }

            var rules = FormHelper.BuildFormPartRulesFromType(type);
            AddFormPartRules(formOptions, rules);

            // Register the option in the host.
            _ = Host.AddFormOption(formOptions);

            // Register part rules in the host runtime (best-effort: host rule store is keyed by name).
            foreach (var rule in rules)
            {
                _ = Host.AddFormPartRule(rule);
            }
        }
    }

    private static void AddFormPartRules(KrFormOptions options, IEnumerable<KrFormPartRule> rules)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                continue;
            }

            var key = string.IsNullOrWhiteSpace(rule.Scope)
                ? rule.Name
                : $"{rule.Scope}::{rule.Name}";

            if (!seen.Add(key))
            {
                continue;
            }

            options.Rules.Add(rule);
        }
    }

    private KrFormOptions? BuildFormOptionsSchema(string? typeName, Type type)
    {
        if (typeName is null)
        {
            return null;
        }

        foreach (var attr in type.GetCustomAttributes<KrBindFormAttribute>(inherit: false))
        {
            var formOptions = FormHelper.ApplyKrPartAttributes(attr);
            formOptions.Name = typeName;
            return formOptions;
        }
        return null;
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
        foreach (var variable in annotations.Values)
        {
            if (variable?.Annotations is null || variable.Annotations.Count == 0)
            {
                continue;
            }

            DispatchComponentAnnotations(variable);
        }
    }

    /// <summary>
    /// Dispatches component annotations for a given variable.
    /// </summary>
    /// <param name="variable">The annotated variable containing annotations.</param>
    private void DispatchComponentAnnotations(OpenApiComponentAnnotationScanner.AnnotatedVariable variable)
    {
        foreach (var annotation in variable.Annotations)
        {
            switch (annotation)
            {
                case OpenApiParameterComponentAttribute paramComponent:
                    ProcessParameterComponent(variable, paramComponent);
                    break;
                case OpenApiRequestBodyComponentAttribute requestBodyComponent:
                    ProcessRequestBodyComponent(variable, requestBodyComponent);
                    break;
                case OpenApiParameterExampleRefAttribute parameterExampleRef:
                    ProcessParameterExampleRef(variable.Name, parameterExampleRef);
                    break;
                case OpenApiRequestBodyExampleRefAttribute requestBodyExampleRef:
                    ProcessRequestBodyExampleRef(variable.Name, requestBodyExampleRef);
                    break;
                case OpenApiExtensionAttribute extensionAttribute:
                    ProcessVariableExtension(variable, extensionAttribute);
                    break;
                case InternalPowershellAttribute powershellAttribute:
                    // Process PowerShell attribute to modify the schema
                    ProcessPowerShellAttribute(variable.Name, powershellAttribute);
                    break;
                case OpenApiResponseComponentAttribute responseComponent:
                    ProcessResponseComponent(variable, responseComponent);
                    break;
                case OpenApiResponseHeaderRefAttribute headerRef:
                    ProcessResponseHeaderRef(variable.Name, headerRef);
                    break;
                case OpenApiResponseLinkRefAttribute linkRef:
                    ProcessResponseLinkRef(variable.Name, linkRef);
                    break;
                case OpenApiResponseExampleRefAttribute exampleRef:
                    ProcessResponseExampleRef(variable.Name, exampleRef);
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Processes an OpenAPI extension annotation for a given variable.
    /// </summary>
    /// <param name="variable"> The annotated variable containing annotations.</param>
    /// <param name="extensionAttribute"> The OpenAPI extension attribute to process.</param>
    private void ProcessVariableExtension(OpenApiComponentAnnotationScanner.AnnotatedVariable variable, OpenApiExtensionAttribute extensionAttribute)
    {
        var extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);

        if (Host.Logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            Host.Logger.Debug("Applying OpenApiExtension '{extensionName}' to function metadata", extensionAttribute.Name);
        }
        // Parse string into a JsonNode tree.
        var node = JsonNode.Parse(extensionAttribute.Json);
        if (node is null)
        {
            Host.Logger.Error("Error parsing OpenAPI extension '{extensionName}': JSON is null", extensionAttribute.Name);
            return;
        }
        extensions[extensionAttribute.Name] = new JsonNodeExtension(node);
        if (variable.Annotations.Any(a => a is OpenApiParameterComponentAttribute))
        {
            var param = GetOrCreateParameterItem(variable.Name, false);
            param.Extensions = extensions;
        }
        else if (variable.Annotations.Any(a => a is OpenApiRequestBodyComponentAttribute))
        {
            var requestBody = GetOrCreateRequestBodyItem(variable.Name, false);
            requestBody.Extensions = extensions;
        }
        else if (variable.Annotations.Any(a => a is OpenApiResponseComponentAttribute))
        {
            var response = GetOrCreateResponseItem(variable.Name, false);
            response.Extensions = extensions;
        }
        else
        {
            Host.Logger.Error("OpenApiExtension '{extensionName}' could not be applied: no matching component found for variable '{variableName}'", extensionAttribute.Name, variable.Name);
            return;
        }
    }

    /// <summary>
    /// Tries to apply the variable type schema to the given OpenAPI response.
    /// </summary>
    /// <param name="response"> The OpenAPI response to apply the schema to.</param>
    /// <param name="variable"> The annotated variable containing annotations.</param>
    /// <param name="responseDescriptor"> The response component attribute describing the response.</param>
    /// <exception cref="InvalidOperationException"> Thrown if the response component does not specify any ContentType.</exception>
    private void TryApplyVariableTypeSchema(
        OpenApiResponse response,
        OpenApiComponentAnnotationScanner.AnnotatedVariable variable,
        OpenApiResponseComponentAttribute responseDescriptor)
    {
        if (variable.VariableType is null)
        {
            return;
        }
        var iSchema = InferPrimitiveSchema(variable.VariableType);
        if (iSchema is OpenApiSchema schema)
        {
            // Apply any schema attributes from the parameter annotation
            ApplyConcreteSchemaAttributes(responseDescriptor, schema);
            // Try to set default value from the variable initial value if not already set
            if (!variable.NoDefault)
            {
                schema.Default = OpenApiJsonNodeFactory.ToNode(variable.InitialValue);
            }
        }

        // Either Schema OR Content, depending on ContentType
        if (responseDescriptor.ContentType.Length == 0)
        {
            throw new InvalidOperationException($"Response component '{variable.Name}' must specify at least one ContentType.");
        }
        // Use Content
        response.Content ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);
        foreach (var contentType in responseDescriptor.ContentType)
        {
            response.Content[contentType] = new OpenApiMediaType { Schema = iSchema };
        }
    }

    private static void ApplyResponseCommonFields(
        OpenApiResponse response,
        OpenApiResponseComponentAttribute responseDescriptor)
    {
        if (responseDescriptor.Summary is not null)
        {
            response.Summary = responseDescriptor.Summary;
        }
        if (responseDescriptor.Description is not null)
        {
            response.Description = responseDescriptor.Description;
        }
    }

    /// <summary>
    /// Generates the OpenAPI document by processing components and building paths and webhooks.
    /// </summary>
    /// <remarks>BuildCallbacks is already handled elsewhere.</remarks>
    /// <remarks>This method sets HasBeenGenerated to true after generation.</remarks>
    public void GenerateDoc()
    {
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

    /// <summary>
    /// Creates an OpenAPI extension in the document from the provided extensions dictionary.
    /// </summary>
    /// <param name="extensions">A dictionary containing the extensions.</param>
    /// <exception cref="ArgumentException">Thrown when the specified extension name is not found in the provided extensions dictionary.</exception>
    public void AddOpenApiExtension(IDictionary? extensions)
    {
        var built = BuildExtensions(extensions);

        if (built is null)
        {
            return;
        }

        Document.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);

        foreach (var kvp in built)
        {
            Document.Extensions[kvp.Key] = kvp.Value;
        }
    }
}
