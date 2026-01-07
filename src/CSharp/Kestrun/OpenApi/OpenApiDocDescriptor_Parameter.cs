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
            parameter.Example = ToNode(param.Example);
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
    /// <param name="variableName">The name of the variable associated with the parameter</param>
    /// <param name="variable">The annotated variable containing metadata about the parameter</param>
    /// <param name="parameterAnnotation">The parameter component annotation</param>
    private void ProcessParameterComponent(
      string variableName,
      OpenApiComponentAnnotationScanner.AnnotatedVariable variable,
      OpenApiParameterComponent parameterAnnotation)
    {
        var parameter = GetOrCreateParameterItem(variableName, parameterAnnotation.Inline);

        ApplyParameterCommonFields(parameter, variableName, parameterAnnotation);

        // Explode defaults to true for "form" and "cookie" styles
        if (parameterAnnotation.Explode || (parameter.Style is ParameterStyle.Form or ParameterStyle.Cookie))
        {
            parameter.Explode = true;
        }

        TryApplyVariableTypeSchema(parameter, variable, parameterAnnotation);
    }

    /// <summary>
    /// Applies common fields from a parameter component annotation to an OpenAPI parameter.
    /// </summary>
    /// <param name="parameter">The OpenApiParameter to modify</param>
    /// <param name="variableName">The name of the variable associated with the parameter</param>
    /// <param name="parameterAnnotation">The parameter component annotation</param>
    private static void ApplyParameterCommonFields(
        OpenApiParameter parameter,
        string variableName,
        OpenApiParameterComponent parameterAnnotation)
    {
        parameter.AllowEmptyValue = parameterAnnotation.AllowEmptyValue;
        parameter.Description = parameterAnnotation.Description;
        parameter.In = parameterAnnotation.In.ToOpenApi();
        parameter.Name = parameterAnnotation.Name ?? variableName;
        parameter.Style = parameterAnnotation.Style?.ToOpenApi();
        parameter.AllowReserved = parameterAnnotation.AllowReserved;
        parameter.Required = parameterAnnotation.Required;
        parameter.Example = OpenApiJsonNodeFactory.FromObject(parameterAnnotation.Example);
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
        OpenApiParameterComponent parameterAnnotation)
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
                schema.Default = OpenApiJsonNodeFactory.FromObject(variable.InitialValue);
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
    /// <param name="variableName">The name of the variable associated with the parameter</param>
    /// <param name="exampleRef">The example reference attribute</param>
    private void ProcessParameterExampleRef(string variableName, OpenApiParameterExampleRefAttribute exampleRef)
    {
        //  var parameter = GetOrCreateParameterItem(variableName, inline: false);
        if (TryGetParameterItem(variableName, out var parameter))
        {
            // Ensure parameter has either Schema or Content
            if (parameter is null || (parameter.Schema is null && parameter.Content is null))
            {
                throw new InvalidOperationException($"Parameter '{variableName}' must have a schema or content defined before adding an example.");
            }
            // Add example to either Schema or Content
            if (parameter.Content is null)
            {
                parameter.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
                // Try to add the example reference
                _ = TryAddExample(parameter.Examples, exampleRef);
            }
            else
            {
                foreach (var iMediaType in parameter.Content.Values)
                {
                    // Try to add the example reference to each media type
                    if (iMediaType is OpenApiMediaType mediaType)
                    {
                        // Ensure Examples dictionary exists
                        mediaType.Examples ??= new Dictionary<string, IOpenApiExample>(StringComparer.Ordinal);
                        // Try to add the example reference
                        _ = TryAddExample(mediaType.Examples, exampleRef);
                    }
                    else if (iMediaType is OpenApiMediaTypeReference)
                    {
                        // Cannot add example reference to a media type reference
                        throw new InvalidOperationException($"Cannot add example reference to media type reference in parameter '{variableName}'.");
                    }
                    else
                    {
                        // Unknown media type
                        throw new InvalidOperationException($"Unknown media type in parameter '{variableName}'.");
                    }
                }
            }
        }
        else
        {
            // Parameter not found
            throw new InvalidOperationException($"Parameter '{variableName}' not found when trying to add example reference.");
        }
    }
    private void ProcessPowerShellAttribute(string variableName, InternalPowershellAttribute powershellAttribute)
    {
        if (TryGetParameterItem(variableName, out var parameter))
        {
            if (parameter is null || (parameter.Schema is null && parameter.Content is null))
            {
                throw new InvalidOperationException($"Parameter '{variableName}' must have a schema or content defined before adding the powershell property.");
            }
            // Add example to either Schema or Content
            if (parameter.Content is null)
            {
                var schema = (OpenApiSchema)parameter.Schema!;
                if (powershellAttribute.MaxItems.HasValue)
                {
                    schema.MaxItems = powershellAttribute.MaxItems;
                }
                if (powershellAttribute.MinItems.HasValue)
                {
                    schema.MinItems = powershellAttribute.MinItems;
                }
                if (!string.IsNullOrEmpty(powershellAttribute.MinRange))
                {
                    schema.Minimum = powershellAttribute.MinRange;
                }
                if (!string.IsNullOrEmpty(powershellAttribute.MaxRange))
                {
                    schema.Maximum = powershellAttribute.MaxRange;
                }
                if (powershellAttribute.MinLength.HasValue)
                {
                    schema.MinLength = powershellAttribute.MinLength;
                }
                if (powershellAttribute.MaxLength.HasValue)
                {
                    schema.MaxLength = powershellAttribute.MaxLength;
                }
                if (!string.IsNullOrEmpty(powershellAttribute.RegexPattern))
                {
                    schema.Pattern = powershellAttribute.RegexPattern;
                }
                if (powershellAttribute.AllowedValues is not null && powershellAttribute.AllowedValues.Count > 0)
                {
                    _ = PowerShellAttributes.ApplyValidateSetAttribute(powershellAttribute.AllowedValues, schema);
                }
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
            else
            {
                Host.Logger.Warning($"Powershell attribute processing is not supported for parameter '{variableName}' with content.");
            }
        }
        else
        {
            // Parameter not found
            throw new InvalidOperationException($"Parameter '{variableName}' not found when trying to add example reference.");
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
    /// <returns>True if the parameter was found; otherwise, false.</returns>
    private bool TryGetParameterItem(string parameterName, out OpenApiParameter? parameter)
    {
        if (TryGetInline(name: parameterName, kind: OpenApiComponentKind.Parameters, out parameter))
        {
            return true;
        }
        else if (TryGetComponent(name: parameterName, kind: OpenApiComponentKind.Parameters, out parameter))
        {
            return true;
        }
        return false;
    }
}
