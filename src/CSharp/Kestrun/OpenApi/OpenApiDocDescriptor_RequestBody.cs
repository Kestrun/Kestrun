using System.Reflection;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Builds request body components from the specified type.
    /// </summary>
    /// <param name="t">The type to build request bodies for.</param>
    private void BuildRequestBodies(Type t)
    {
        Document.Components!.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal);
        var componentSchema = BuildSchemaForType(t);
        var requestBody = new OpenApiRequestBody();
        // Apply request body component attribute if present
        var name = ApplyRequestBodyComponent(t.GetCustomAttribute<OpenApiRequestBodyComponentAttribute>(), requestBody, componentSchema);

        // Apply example references if any (handle multiple attributes)
        var exampleRefs = t.GetCustomAttributes<OpenApiExampleRefAttribute>();
        foreach (var exRef in exampleRefs)
        {
            ApplyRequestBodyExampleRef(exRef, requestBody);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = t.Name; // fallback to type name if no explicit key was provided
        }
        // Register the request body component
        _ = Document.Components!.RequestBodies.TryAdd(name, requestBody);
    }

    /// <summary>
    /// Applies the request body component attribute to the request body.
    /// </summary>
    /// <param name="bodyAttribute"> The request body component attribute to apply.</param>
    /// <param name="requestBody"> The request body to apply the attribute to.</param>
    /// <param name="schema"> The schema to associate with the request body.</param>
    /// <returns>The name of the request body component if explicitly specified; otherwise, null.</returns>
    private static string? ApplyRequestBodyComponent(OpenApiRequestBodyComponentAttribute? bodyAttribute, OpenApiRequestBody requestBody, IOpenApiSchema schema)
    {
        if (bodyAttribute is null)
        {
            return null;
        }
        var name = string.Empty;
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
        requestBody.Content ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);

        var mediaType = new OpenApiMediaType { Schema = schema };
        if (bodyAttribute.Example is not null)
        {
            mediaType.Example = OpenApiJsonNodeFactory.ToNode(bodyAttribute.Example);
        }

        foreach (var ct in bodyAttribute.ContentType)
        {
            requestBody.Content[ct] = mediaType;
        }

        return name;
    }

    /// <summary>
    /// Applies example references to the request body.
    /// </summary>
    /// <param name="exRef">The example reference attribute to apply.</param>
    /// <param name="requestBody">The request body to apply the example references to.</param>
    private void ApplyRequestBodyExampleRef(OpenApiExampleRefAttribute? exRef, OpenApiRequestBody requestBody)
    {
        if (exRef is null)
        {
            return;
        }
        requestBody.Content ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);
        var targets = ResolveExampleContentTypes(exRef, requestBody);
        foreach (var ct in targets)
        {
            var mediaType = requestBody.Content.TryGetValue(ct, out var existing)
                ? existing
                : (requestBody.Content[ct] = new OpenApiMediaType());

            if (mediaType is not OpenApiMediaType concreteMedia)
            {
                throw new InvalidOperationException($"Expected OpenApiMediaType for content type '{ct}', got '{mediaType.GetType().FullName}'.");
            }

            concreteMedia.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
            concreteMedia.Examples[exRef.Key] = exRef.Inline
                ? CloneExampleOrThrow(exRef.ReferenceId)
                : new OpenApiExampleReference(exRef.ReferenceId);
        }
    }

    /// <summary>
    /// Resolves the content types to apply example references to.
    /// </summary>
    /// <param name="exRef">The example reference attribute.</param>
    /// <param name="requestBody">The request body.</param>
    /// <returns>The list of content types to apply the example references to.</returns>
    private static IEnumerable<string> ResolveExampleContentTypes(OpenApiExampleRefAttribute exRef, OpenApiRequestBody requestBody)
    {
        var keys = exRef.ContentType is null ? (requestBody.Content?.Keys ?? Array.Empty<string>()) : exRef.ContentType;
        return keys.Count == 0 ? ["application/json"] : (IEnumerable<string>)keys;
    }

    /// <summary>
    /// Clones an example from components or throws if not found.
    /// </summary>
    /// <param name="referenceId"> The reference ID of the example to clone.</param>
    /// <returns>The cloned example.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the example reference cannot be found or is not an OpenApiExample.</exception>
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private IOpenApiExample CloneExampleOrThrow(string referenceId)
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
    {
        return Document.Components?.Examples == null || !Document.Components.Examples.TryGetValue(referenceId, out var value)
            ? throw new InvalidOperationException($"Example reference '{referenceId}' cannot be embedded because it was not found in components.")
            : value is not OpenApiExample example
            ? throw new InvalidOperationException($"Example reference '{referenceId}' cannot be embedded because it is not an OpenApiExample.")
            : (IOpenApiExample)example.Clone();
    }



    #region Request Body Component Processing
    /// <summary>
    /// Processes a request body component annotation to create or update an OpenAPI request body.
    /// </summary>
    /// <param name="variable">The annotated variable containing metadata about the request body</param>
    /// <param name="requestBodyDescriptor">The request body component annotation</param>
    private void ProcessRequestBodyComponent(
      OpenApiComponentAnnotationScanner.AnnotatedVariable variable,
      OpenApiRequestBodyComponentAttribute requestBodyDescriptor)
    {
        var key = requestBodyDescriptor.Key ?? variable.Name;
        var requestBody = GetOrCreateRequestBodyItem(key, requestBodyDescriptor.Inline);

        ApplyRequestBodyCommonFields(requestBody, requestBodyDescriptor);

        TryApplyVariableTypeSchema(requestBody, variable, requestBodyDescriptor);
    }

    /// <summary>
    /// Applies common fields from a request body component annotation to an OpenAPI request body.
    /// </summary>
    /// <param name="requestBody">The OpenApiRequestBody to modify</param>
    /// <param name="requestBodyAnnotation">The request body component annotation</param>
    private static void ApplyRequestBodyCommonFields(
        OpenApiRequestBody requestBody,
        OpenApiRequestBodyComponentAttribute requestBodyAnnotation)
    {
        requestBody.Description = requestBodyAnnotation.Description;

        requestBody.Required = requestBodyAnnotation.Required;
    }



    /// <summary>
    /// Tries to apply the variable type schema to an OpenAPI request body.
    /// </summary>
    /// <param name="requestBody">The OpenApiRequestBody to modify</param>
    /// <param name="variable">The annotated variable containing metadata about the request body</param>
    /// <param name="requestBodyAnnotation">The request body component annotation</param>
    private void TryApplyVariableTypeSchema(
         OpenApiRequestBody requestBody,
       OpenApiComponentAnnotationScanner.AnnotatedVariable variable,
        OpenApiRequestBodyComponentAttribute requestBodyAnnotation)
    {
        if (variable.VariableType is null)
        {
            return;
        }
        var iSchema = InferPrimitiveSchema(variable.VariableType);
        if (iSchema is OpenApiSchema schema)
        {
            //Todo: add powershell attribute support
            //PowerShellAttributes.ApplyPowerShellAttributes(variable.PropertyInfo, schema);
            // Apply any schema attributes from the request body annotation
            ApplyConcreteSchemaAttributes(requestBodyAnnotation, schema);
            // Try to set default value from the variable initial value if not already set
            if (!variable.NoDefault)
            {
                schema.Default = OpenApiJsonNodeFactory.ToNode(variable.InitialValue);
            }
        }
        if (requestBodyAnnotation.ContentType is null || requestBodyAnnotation.ContentType.Length == 0)
        {
            // Fallback to application/json if no content type specified
            requestBodyAnnotation.ContentType = ["application/json"];
        }

        // Use Content
        requestBody.Content ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);
        foreach (var ct in requestBodyAnnotation.ContentType)
        {
            requestBody.Content[ct] = new OpenApiMediaType { Schema = iSchema };
        }

    }


    /// <summary>
    /// Processes a request body example reference annotation to add an example to an OpenAPI request body.
    /// </summary>
    /// <param name="variableName">The name of the variable associated with the request body.</param>
    /// <param name="exampleRef">The example reference attribute.</param>
    /// <exception cref="InvalidOperationException">Thrown when the request body does not exist, lacks schema/content, or media types are unsupported.</exception>
    private void ProcessRequestBodyExampleRef(string variableName, OpenApiRequestBodyExampleRefAttribute exampleRef)
    {
        if (!TryGetRequestBodyItem(variableName, out var requestBody))
        {
            throw new InvalidOperationException($"Request body '{variableName}' not found when trying to add example reference.");
        }

        if (requestBody!.Content is null)
        {
            AddExampleToRequestBodyExamples(requestBody, exampleRef);
            return;
        }

        AddExamplesToContentMediaTypes(requestBody, exampleRef, variableName);
    }

    /// <summary>
    /// Ensures the request body Examples dictionary exists and attempts to add the example reference.
    /// </summary>
    /// <param name="requestBody">The OpenAPI request body to modify.</param>
    /// <param name="exampleRef">The example reference attribute.</param>
    private void AddExampleToRequestBodyExamples(OpenApiRequestBody requestBody, OpenApiRequestBodyExampleRefAttribute exampleRef)
    {
        foreach (var mediaType in requestBody.Content!.Values)
        {
            if (mediaType is OpenApiMediaType concreteMedia)
            {
                concreteMedia.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
                _ = TryAddExample(concreteMedia.Examples, exampleRef);
            }
        }
    }
    #endregion

    /// <summary>
    /// Gets or creates an OpenAPI request body item in either inline or document components.
    /// </summary>
    /// <param name="requestBodyName">The name of the request body.</param>
    /// <param name="inline">Whether to use inline components or document components.</param>
    /// <returns>The OpenApiRequestBody item.</returns>
    private OpenApiRequestBody GetOrCreateRequestBodyItem(string requestBodyName, bool inline)
    {
        IDictionary<string, IOpenApiRequestBody> requestBodies;
        // Determine whether to use inline components or document components
        if (inline)
        {
            // Use inline components
            InlineComponents.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal);
            requestBodies = InlineComponents.RequestBodies;
        }
        else
        {
            // Use document components
            Document.Components ??= new OpenApiComponents();
            Document.Components.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(StringComparer.Ordinal);
            requestBodies = Document.Components.RequestBodies;
        }
        // Retrieve or create the request body item
        if (!requestBodies.TryGetValue(requestBodyName, out var requestBodyInterface) || requestBodyInterface is null)
        {
            // Create a new OpenApiRequestBody if it doesn't exist
            requestBodyInterface = new OpenApiRequestBody();
            requestBodies[requestBodyName] = requestBodyInterface;
        }
        // return the request body item
        return (OpenApiRequestBody)requestBodyInterface;
    }

    /// <summary>
    /// Tries to get a request body by name from either inline or document components.
    /// </summary>
    /// <param name="requestBodyName">The name of the request body to retrieve.</param>
    /// <param name="requestBody">The retrieved request body if found; otherwise, null.</param>
    /// <param name="isInline">Indicates whether the request body was found in inline components.</param>
    /// <returns>True if the request body was found; otherwise, false.</returns>
    private bool TryGetRequestBodyItem(string requestBodyName, out OpenApiRequestBody? requestBody, out bool isInline)
    {
        if (TryGetInline(name: requestBodyName, kind: OpenApiComponentKind.RequestBodies, out requestBody))
        {
            isInline = true;
            return true;
        }
        else if (TryGetComponent(name: requestBodyName, kind: OpenApiComponentKind.RequestBodies, out requestBody))
        {
            isInline = false;
            return true;
        }
        requestBody = null;
        isInline = false;
        return false;
    }

    /// <summary>
    /// Tries to get a request body by name from either inline or document components.
    /// </summary>
    /// <param name="requestBodyName">The name of the request body to retrieve.</param>
    /// <param name="requestBody">The retrieved request body if found; otherwise, null.</param>
    /// <returns>True if the request body was found; otherwise, false.</returns>
    private bool TryGetRequestBodyItem(string requestBodyName, out OpenApiRequestBody? requestBody) =>
    TryGetRequestBodyItem(requestBodyName, out requestBody, out _);
}
