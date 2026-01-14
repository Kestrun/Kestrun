using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

/// <summary>
/// Generates OpenAPI v2 (Swagger) documents from C# types decorated with OpenApiSchema attributes.
/// </summary>
public partial class OpenApiDocDescriptor
{
    /// <summary>
    /// Creates an OpenAPI parameter from a given attribute.
    /// </summary>
    /// <param name="attr">The attribute to create the parameter from</param>
    /// <param name="parameter">The OpenApiParameter object to populate</param>
    /// <returns>True if the parameter was created successfully, otherwise false</returns>
    /// <exception cref="InvalidOperationException">Thrown when an example reference cannot be embedded due to missing or invalid components.</exception>
    private bool CreateParameterFromAttribute(KestrunAnnotation attr, OpenApiParameter parameter)
    {
        switch (attr)
        {
            case OpenApiParameterAttribute param:
                ApplyParameterAttribute(param, parameter);
                break;

            case OpenApiExampleRefAttribute exRef:
                ApplyExampleRefAttribute(exRef, parameter);
                break;

            default:
                return false; // unrecognized attribute type
        }
        return true;
    }

    /// <summary>
    /// Applies an OpenApiParameterAttribute to an OpenApiParameter.
    /// </summary>
    /// <param name="param">The OpenApiParameterAttribute to apply</param>
    /// <param name="parameter">The OpenApiParameter to modify</param>
    private static void ApplyParameterAttribute(OpenApiParameterAttribute param, OpenApiParameter parameter)
    {
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
            parameter.Example = OpenApiJsonNodeFactory.ToNode(param.Example);
        }
    }

    /// <summary>
    /// Applies an example reference attribute to an OpenAPI parameter.
    /// </summary>
    /// <param name="exRef">The OpenApiExampleRefAttribute to apply</param>
    /// <param name="parameter">The OpenApiParameter to modify</param>
    private void ApplyExampleRefAttribute(OpenApiExampleRefAttribute exRef, OpenApiParameter parameter)
    {
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
    }

    #region Parameter Component Processing
    /// <summary>
    /// Processes a parameter component annotation to create or update an OpenAPI parameter.
    /// </summary>
    /// <param name="variable">The annotated variable containing metadata about the parameter</param>
    /// <param name="parameterDescriptor">The parameter component annotation</param>
    private void ProcessParameterComponent(
      OpenApiComponentAnnotationScanner.AnnotatedVariable variable,
      OpenApiParameterComponentAttribute parameterDescriptor)
    {
        var key = parameterDescriptor.Key ?? variable.Name;
        var parameter = GetOrCreateParameterItem(key, parameterDescriptor.Inline);

        ApplyParameterCommonFields(parameter, parameterDescriptor);

        // Explode defaults to true for "form" and "cookie" styles
        if (parameterDescriptor.Explode || (parameter.Style is ParameterStyle.Form or ParameterStyle.Cookie))
        {
            parameter.Explode = true;
        }
        // Set the parameter name from the variable name
        parameter.Name = variable.Name;
        TryApplyVariableTypeSchema(parameter, variable, parameterDescriptor);
    }

    /// <summary>
    /// Applies common fields from a parameter component annotation to an OpenAPI parameter.
    /// </summary>
    /// <param name="parameter">The OpenApiParameter to modify</param>
    /// <param name="parameterAnnotation">The parameter component annotation</param>
    private static void ApplyParameterCommonFields(
        OpenApiParameter parameter,
        OpenApiParameterComponentAttribute parameterAnnotation)
    {
        parameter.AllowEmptyValue = parameterAnnotation.AllowEmptyValue;
        parameter.Description = parameterAnnotation.Description;
        parameter.In = parameterAnnotation.In.ToOpenApi();
        parameter.Style = parameterAnnotation.Style?.ToOpenApi();
        parameter.AllowReserved = parameterAnnotation.AllowReserved;
        parameter.Required = parameterAnnotation.Required;
        parameter.Example = OpenApiJsonNodeFactory.ToNode(parameterAnnotation.Example);
        parameter.Deprecated = parameterAnnotation.Deprecated;
    }

    /// <summary>
    /// Tries to apply the variable type schema to an OpenAPI parameter.
    /// </summary>
    /// <param name="parameter">The OpenApiParameter to modify</param>
    /// <param name="variable">The annotated variable containing metadata about the parameter</param>
    /// <param name="parameterAnnotation">The parameter component annotation</param>
    private void TryApplyVariableTypeSchema(
         OpenApiParameter parameter,
       OpenApiComponentAnnotationScanner.AnnotatedVariable variable,
        OpenApiParameterComponentAttribute parameterAnnotation)
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
            // Apply any schema attributes from the parameter annotation
            ApplyConcreteSchemaAttributes(parameterAnnotation, schema);
            // Try to set default value from the variable initial value if not already set
            if (!variable.NoDefault)
            {
                schema.Default = OpenApiJsonNodeFactory.ToNode(variable.InitialValue);
            }
        }

        // Either Schema OR Content, depending on ContentType
        if (string.IsNullOrWhiteSpace(parameterAnnotation.ContentType))
        {
            parameter.Schema = iSchema;
            return;
        }
        // Use Content
        parameter.Content ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);
        parameter.Content[parameterAnnotation.ContentType] = new OpenApiMediaType { Schema = iSchema };
    }

    /// <summary>
    /// Processes a parameter example reference annotation to add an example to an OpenAPI parameter.
    /// </summary>
    /// <param name="variableName">The name of the variable associated with the parameter.</param>
    /// <param name="exampleRef">The example reference attribute.</param>
    /// <exception cref="InvalidOperationException">Thrown when the parameter does not exist, lacks schema/content, or media types are unsupported.</exception>
    private void ProcessParameterExampleRef(string variableName, OpenApiParameterExampleRefAttribute exampleRef)
    {
        if (!TryGetParameterItem(variableName, out var parameter))
        {
            throw new InvalidOperationException($"Parameter '{variableName}' not found when trying to add example reference.");
        }

        ValidateParameterHasSchemaOrContent(variableName, parameter);

        if (parameter!.Content is null)
        {
            AddExampleToParameterExamples(parameter, exampleRef);
            return;
        }

        AddExamplesToContentMediaTypes(parameter, exampleRef, variableName);
    }

    /// <summary>
    /// Validates that the parameter exists and has either Schema or Content defined.
    /// </summary>
    /// <param name="variableName">The variable name associated with the parameter.</param>
    /// <param name="parameter">The parameter to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the parameter is null or lacks both Schema and Content.</exception>
    private static void ValidateParameterHasSchemaOrContent(string variableName, OpenApiParameter? parameter)
    {
        if (parameter is null || (parameter.Schema is null && parameter.Content is null))
        {
            throw new InvalidOperationException($"Parameter '{variableName}' must have a schema or content defined before adding an example.");
        }
    }

    /// <summary>
    /// Ensures the parameter Examples dictionary exists and attempts to add the example reference.
    /// </summary>
    /// <param name="parameter">The OpenAPI parameter to modify.</param>
    /// <param name="exampleRef">The example reference attribute.</param>
    private void AddExampleToParameterExamples(OpenApiParameter parameter, OpenApiParameterExampleRefAttribute exampleRef)
    {
        parameter.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
        _ = TryAddExample(parameter.Examples, exampleRef);
    }

    /// <summary>
    /// Iterates the parameter's content media types and adds the example reference to each concrete media type.
    /// </summary>
    /// <param name="parameter">The OpenAPI parameter with content.</param>
    /// <param name="exampleRef">The example reference attribute.</param>
    /// <param name="variableName">The variable name used for error messages.</param>
    /// <exception cref="InvalidOperationException">Thrown when encountering a media type reference or an unknown media type.</exception>
    private void AddExamplesToContentMediaTypes(OpenApiParameter parameter, IOpenApiExampleAttribute exampleRef, string variableName)
    {
        foreach (var iMediaType in parameter.Content!.Values)
        {
            if (iMediaType is OpenApiMediaType mediaType)
            {
                mediaType.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
                _ = TryAddExample(mediaType.Examples, exampleRef);
                continue;
            }

            if (iMediaType is OpenApiMediaTypeReference)
            {
                throw new InvalidOperationException($"Cannot add example reference to media type reference in parameter '{variableName}'.");
            }

            throw new InvalidOperationException($"Unknown media type in parameter '{variableName}'.");
        }
    }

    /// <summary>
    /// Iterates the request body's content media types and adds the example reference to each concrete media type.
    /// </summary>
    /// <param name="requestBody">The OpenAPI request body with content.</param>
    /// <param name="exampleRef">The example reference attribute.</param>
    /// <param name="variableName">The variable name used for error messages.</param>
    /// <exception cref="InvalidOperationException">Thrown when encountering a media type reference or an unknown media type.</exception>
    private void AddExamplesToContentMediaTypes(OpenApiRequestBody requestBody, IOpenApiExampleAttribute exampleRef, string variableName)
    {
        foreach (var iMediaType in requestBody.Content!.Values)
        {
            try
            {
                if (iMediaType is OpenApiMediaType mediaType)
                {
                    mediaType.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
                    if (!TryAddExample(mediaType.Examples, exampleRef))
                    {
                        throw new InvalidOperationException($"Failed to add example reference '{exampleRef.ReferenceId}' to media type in request body '{variableName}'.");
                    }
                    continue;
                }

                if (iMediaType is OpenApiMediaTypeReference)
                {
                    throw new InvalidOperationException($"Cannot add example reference to media type reference in request body '{variableName}'.");
                }

                throw new InvalidOperationException($"Unknown media type in request body '{variableName}'.");
            }
            catch (Exception ex)
            {
                Host.Logger.Error("Error adding example reference to request body {variableName}: {ex.Message}", variableName, ex.Message);
            }
        }
    }

    /// <summary>
    /// Processes a PowerShell attribute to add validation constraints to an OpenAPI parameter.
    /// </summary>
    /// <param name="variableName">The name of the variable associated with the parameter</param>
    /// <param name="powershellAttribute">The PowerShell attribute containing validation constraints</param>
    /// <exception cref="InvalidOperationException">Thrown if the parameter does not have a schema or content defined before adding the PowerShell property.</exception>
    private void ProcessPowerShellAttribute(string variableName, InternalPowershellAttribute powershellAttribute)
    {
        if (TryGetParameterItem(variableName, out var parameter))
        {
            if (parameter is null || (parameter.Schema is null && parameter.Content is null))
            {
                throw new InvalidOperationException($"Parameter '{variableName}' must have a schema or content defined before adding the powershell property.");
            }

            if (parameter.Content is not null)
            {
                foreach (var mediaType in parameter.Content.Values)
                {
                    if (mediaType.Schema is not OpenApiSchema mSchema)
                    {
                        Host.Logger.Warning($"Powershell attribute processing is not supported for parameter '{variableName}' with non-concrete schema.");
                        continue;
                    }

                    ApplyPowerShellAttributesToSchema(mSchema, powershellAttribute);
                }
                return;
            }
            var schema = (OpenApiSchema)parameter.Schema!;
            ApplyPowerShellAttributesToSchema(schema, powershellAttribute);
            return;
        }

        if (TryGetRequestBodyItem(variableName, out var requestBody))
        {
            if (requestBody is null || requestBody.Content is null)
            {
                throw new InvalidOperationException($"RequestBody '{variableName}' must have a content defined before adding the powershell property.");
            }
            foreach (var mediaType in requestBody.Content.Values)
            {
                if (mediaType.Schema is not OpenApiSchema schema)
                {
                    Host.Logger.Warning($"Powershell attribute processing is not supported for request body '{variableName}' with non-concrete schema.");
                    continue;
                }

                ApplyPowerShellAttributesToSchema(schema, powershellAttribute);
            }
            return;
        }
        Host.Logger.Error("Parameter or RequestBody '{variableName}' not found when trying to add PowerShell attribute.", variableName);
    }

    /// <summary>
    /// Applies PowerShell validation attributes to an OpenAPI schema.
    /// </summary>
    /// <param name="schema">The OpenAPI schema to modify.</param>
    /// <param name="powershellAttribute">The PowerShell attribute containing validation constraints.</param>
    private static void ApplyPowerShellAttributesToSchema(OpenApiSchema schema, InternalPowershellAttribute powershellAttribute)
    {
        ApplyItemConstraints(schema, powershellAttribute);
        ApplyRangeConstraints(schema, powershellAttribute);
        ApplyLengthConstraints(schema, powershellAttribute);
        ApplyPatternConstraints(schema, powershellAttribute);
        ApplyAllowedValuesConstraints(schema, powershellAttribute);
        ApplyNullabilityConstraints(schema, powershellAttribute);
    }

    /// <summary>
    /// Applies item count constraints (MinItems, MaxItems) to a schema.
    /// </summary>
    /// <param name="schema">The schema to modify.</param>
    /// <param name="powershellAttribute">The PowerShell attribute containing constraints.</param>
    private static void ApplyItemConstraints(OpenApiSchema schema, InternalPowershellAttribute powershellAttribute)
    {
        if (powershellAttribute.MaxItems.HasValue)
        {
            schema.MaxItems = powershellAttribute.MaxItems;
        }
        if (powershellAttribute.MinItems.HasValue)
        {
            schema.MinItems = powershellAttribute.MinItems;
        }
    }

    /// <summary>
    /// Applies range constraints (Minimum, Maximum) to a schema.
    /// </summary>
    /// <param name="schema">The schema to modify.</param>
    /// <param name="powershellAttribute">The PowerShell attribute containing constraints.</param>
    private static void ApplyRangeConstraints(OpenApiSchema schema, InternalPowershellAttribute powershellAttribute)
    {
        if (!string.IsNullOrEmpty(powershellAttribute.MinRange))
        {
            schema.Minimum = powershellAttribute.MinRange;
        }
        if (!string.IsNullOrEmpty(powershellAttribute.MaxRange))
        {
            schema.Maximum = powershellAttribute.MaxRange;
        }
    }

    /// <summary>
    /// Applies length constraints (MinLength, MaxLength) to a schema.
    /// </summary>
    /// <param name="schema">The schema to modify.</param>
    /// <param name="powershellAttribute">The PowerShell attribute containing constraints.</param>
    private static void ApplyLengthConstraints(OpenApiSchema schema, InternalPowershellAttribute powershellAttribute)
    {
        if (powershellAttribute.MinLength.HasValue)
        {
            schema.MinLength = powershellAttribute.MinLength;
        }
        if (powershellAttribute.MaxLength.HasValue)
        {
            schema.MaxLength = powershellAttribute.MaxLength;
        }
    }

    /// <summary>
    /// Applies pattern constraints (regex) to a schema.
    /// </summary>
    /// <param name="schema">The schema to modify.</param>
    /// <param name="powershellAttribute">The PowerShell attribute containing constraints.</param>
    private static void ApplyPatternConstraints(OpenApiSchema schema, InternalPowershellAttribute powershellAttribute)
    {
        if (!string.IsNullOrEmpty(powershellAttribute.RegexPattern))
        {
            schema.Pattern = powershellAttribute.RegexPattern;
        }
    }

    /// <summary>
    /// Applies allowed values (enum) constraints to a schema.
    /// </summary>
    /// <param name="schema">The schema to modify.</param>
    /// <param name="powershellAttribute">The PowerShell attribute containing constraints.</param>
    private static void ApplyAllowedValuesConstraints(OpenApiSchema schema, InternalPowershellAttribute powershellAttribute)
    {
        if (powershellAttribute.AllowedValues is not null && powershellAttribute.AllowedValues.Count > 0)
        {
            _ = PowerShellAttributes.ApplyValidateSetAttribute(powershellAttribute.AllowedValues, schema);
        }
    }

    /// <summary>
    /// Applies nullability constraints (ValidateNotNull, ValidateNotNullOrEmpty, ValidateNotNullOrWhiteSpace) to a schema.
    /// </summary>
    /// <param name="schema">The schema to modify.</param>
    /// <param name="powershellAttribute">The PowerShell attribute containing constraints.</param>
    private static void ApplyNullabilityConstraints(OpenApiSchema schema, InternalPowershellAttribute powershellAttribute)
    {
        if (powershellAttribute.ValidateNotNullOrEmptyAttribute is not null)
        {
            _ = PowerShellAttributes.ApplyNotNullOrEmpty(schema);
        }

        if (powershellAttribute.ValidateNotNullAttribute is not null)
        {
            _ = PowerShellAttributes.ApplyNotNull(schema);
        }

        if (powershellAttribute.ValidateNotNullOrWhiteSpaceAttribute is not null)
        {
            _ = PowerShellAttributes.ApplyNotNullOrWhiteSpace(schema);
        }
    }

    #endregion

    /// <summary>
    /// Gets or creates an OpenAPI parameter item in either inline or document components.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="inline">Whether to use inline components or document components.</param>
    /// <returns>The OpenApiParameter item.</returns>
    private OpenApiParameter GetOrCreateParameterItem(string parameterName, bool inline)
    {
        IDictionary<string, IOpenApiParameter> parameters;
        // Determine whether to use inline components or document components
        if (inline)
        {
            // Use inline components
            InlineComponents.Parameters ??= new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal);
            parameters = InlineComponents.Parameters;
        }
        else
        {
            // Use document components
            Document.Components ??= new OpenApiComponents();
            Document.Components.Parameters ??= new Dictionary<string, IOpenApiParameter>(StringComparer.Ordinal);
            parameters = Document.Components.Parameters;
        }
        // Retrieve or create the parameter item
        if (!parameters.TryGetValue(parameterName, out var parameterInterface) || parameterInterface is null)
        {
            // Create a new OpenApiParameter if it doesn't exist
            parameterInterface = new OpenApiParameter();
            parameters[parameterName] = parameterInterface;
        }
        // return the parameter item
        return (OpenApiParameter)parameterInterface;
    }

    /// <summary>
    /// Tries to get a parameter by name from either inline or document components.
    /// </summary>
    /// <param name="parameterName">The name of the parameter to retrieve.</param>
    /// <param name="parameter">The retrieved parameter if found; otherwise, null.</param>
    /// <param name="isInline">Indicates whether the parameter was found in inline components.</param>
    /// <returns>True if the parameter was found; otherwise, false.</returns>
    private bool TryGetParameterItem(string parameterName, out OpenApiParameter? parameter, out bool isInline)
    {
        if (TryGetInline(name: parameterName, kind: OpenApiComponentKind.Parameters, out parameter))
        {
            isInline = true;
            return true;
        }
        else if (TryGetComponent(name: parameterName, kind: OpenApiComponentKind.Parameters, out parameter))
        {
            isInline = false;
            return true;
        }
        parameter = null;
        isInline = false;
        return false;
    }

    /// <summary>
    /// Tries to get a parameter by name from either inline or document components.
    /// </summary>
    /// <param name="parameterName">The name of the parameter to retrieve.</param>
    /// <param name="parameter">The retrieved parameter if found; otherwise, null.</param>
    /// <returns>True if the parameter was found; otherwise, false.</returns>
    private bool TryGetParameterItem(string parameterName, out OpenApiParameter? parameter) =>
    TryGetParameterItem(parameterName, out parameter, out _);
}
