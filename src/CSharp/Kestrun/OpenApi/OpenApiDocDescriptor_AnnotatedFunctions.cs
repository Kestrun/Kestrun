using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Management.Automation.Language;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Languages;
using Kestrun.Utilities;
using Microsoft.OpenApi;

namespace Kestrun.OpenApi;

public partial class OpenApiDocDescriptor
{
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
            ProcessFunction(func);
        }
    }

    /// <summary>
    /// Processes a single PowerShell function, extracting OpenAPI annotations and configuring the host accordingly.
    /// </summary>
    /// <param name="func">The function information.</param>
    private void ProcessFunction(FunctionInfo func)
    {
        try
        {
            var help = func.GetHelp();
            var sb = func.ScriptBlock;
            if (sb is null)
            {
                return;
            }

            var attrs = sb.Attributes;
            if (attrs.Count == 0)
            {
                return;
            }

            var openApiMetadata = new OpenAPIMetadata();
            var routeOptions = new MapRouteOptions();
            var parsedVerb = ProcessFunctionAttributes(func, help!, attrs, routeOptions, openApiMetadata);

            ProcessParameters(func, help!, routeOptions, openApiMetadata);

            EnsureDefaultResponses(openApiMetadata);
            FinalizeRouteOptions(func, sb, openApiMetadata, routeOptions, parsedVerb);
        }
        catch (Exception ex)
        {
            Host.Logger.Error("Error loading OpenAPI annotated function '{funcName}': {message}", func.Name, ex.Message);
        }
    }

    /// <summary>
    /// Processes the OpenAPI-related attributes on the specified function.
    /// </summary>
    /// <param name="func">The function information.</param>
    /// <param name="help">The comment help information.</param>
    /// <param name="attrs">The collection of attributes applied to the function.</param>
    /// <param name="routeOptions">The route options to configure.</param>
    /// <param name="openApiMetadata">The OpenAPI metadata to populate.</param>
    /// <returns>The parsed HTTP verb for the function.</returns>
    private HttpVerb ProcessFunctionAttributes(
        FunctionInfo func,
        CommentHelpInfo help,
        IReadOnlyCollection<Attribute> attrs,
        MapRouteOptions routeOptions,
        OpenAPIMetadata openApiMetadata)
    {
        var parsedVerb = HttpVerb.Get;

        foreach (var attr in attrs)
        {
            try
            {
                switch (attr)
                {
                    case OpenApiPath path:
                        parsedVerb = ApplyPathAttribute(func, help, routeOptions, openApiMetadata, parsedVerb, path);
                        break;
                    case OpenApiResponseRefAttribute responseRef:
                        ApplyResponseRefAttribute(openApiMetadata, responseRef);
                        break;
                    case OpenApiResponseAttribute responseAttr:
                        ApplyResponseAttribute(openApiMetadata, responseAttr);
                        break;
                    case OpenApiResponseExampleRefAttribute responseAttr:
                        ApplyResponseAttribute(openApiMetadata, responseAttr);
                        break;
                    case OpenApiPropertyAttribute propertyAttr:
                        ApplyPropertyAttribute(openApiMetadata, propertyAttr);
                        break;
                    case OpenApiAuthorizationAttribute authAttr:
                        ApplyAuthorizationAttribute(routeOptions, openApiMetadata, authAttr);
                        break;
                    case KestrunAnnotation ka:
                        throw new InvalidOperationException($"Unhandled Kestrun annotation: {ka.GetType().Name}");
                }
            }
            catch (InvalidOperationException ex)
            {
                Host.Logger.Error("Error processing OpenApiPath attribute on function '{funcName}': {message}", func.Name, ex.Message);
            }
            catch (Exception ex)
            {
                Host.Logger.Error("Error processing OpenApiPath attribute on function '{funcName}': {message}", func.Name, ex.Message);
            }
        }

        return parsedVerb;
    }

    /// <summary>
    /// Applies the OpenApiPath attribute to the function's route options and metadata.
    /// </summary>
    /// <param name="func">The function information.</param>
    /// <param name="help">The comment help information.</param>
    /// <param name="routeOptions">The route options to configure.</param>
    /// <param name="metadata">The OpenAPI metadata to populate.</param>
    /// <param name="parsedVerb">The currently parsed HTTP verb.</param>
    /// <param name="oaPath">The OpenApiPath attribute instance.</param>
    /// <returns>The updated HTTP verb after processing the attribute.</returns>
    private static HttpVerb ApplyPathAttribute(
        FunctionInfo func,
        CommentHelpInfo help,
        MapRouteOptions routeOptions,
        OpenAPIMetadata metadata,
        HttpVerb parsedVerb,
        OpenApiPath oaPath)
    {
        var httpVerb = oaPath.HttpVerb ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(httpVerb))
        {
            parsedVerb = HttpVerbExtensions.FromMethodString(httpVerb);
            routeOptions.HttpVerbs.Add(parsedVerb);
        }

        if (!string.IsNullOrWhiteSpace(oaPath.Pattern))
        {
            routeOptions.Pattern = oaPath.Pattern;
            metadata.Pattern = oaPath.Pattern;
        }

        metadata.Summary = ChooseFirstNonEmpty(oaPath.Summary, help.GetSynopsis());
        metadata.Description = ChooseFirstNonEmpty(oaPath.Description, help.GetDescription());
        metadata.Tags = [.. oaPath.Tags];
        metadata.OperationId = oaPath.OperationId is null
            ? func.Name
            : string.IsNullOrWhiteSpace(oaPath.OperationId) ? metadata.OperationId : oaPath.OperationId;
        // Apply deprecated flag if specified
        metadata.Deprecated |= oaPath.Deprecated;
        // Apply Cors policy name if specified
        metadata.CorsPolicy = oaPath.CorsPolicy;
        return parsedVerb;
    }

    /// <summary>
    /// Chooses the first non-empty string from the provided values, normalizing newlines.
    /// </summary>
    /// <param name="values">An array of string values to evaluate.</param>
    /// <returns>The first non-empty string with normalized newlines, or null if all are null or whitespace.</returns>
    private static string? ChooseFirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return NormalizeNewlines(value);
            }
        }

        return null;
    }

    /// <summary>
    /// Normalizes newlines in the given string to use '\n' only.
    /// </summary>
    /// <param name="value">The string to normalize.</param>
    /// <returns>The normalized string.</returns>
    private static string? NormalizeNewlines(string? value) => value?.Replace("\r\n", "\n");
    private void ApplyResponseRefAttribute(OpenAPIMetadata metadata, OpenApiResponseRefAttribute attribute)
    {
        metadata.Responses ??= [];
        IOpenApiResponse response = attribute.Inline
            ? GetResponse(attribute.ReferenceId).Clone()
            : new OpenApiResponseReference(attribute.ReferenceId);

        if (attribute.Description is not null)
        {
            response.Description = attribute.Description;
        }

        if (metadata.Responses.ContainsKey(attribute.StatusCode))
        {
            throw new InvalidOperationException($"Response for status code '{attribute.StatusCode}' is already defined for this operation.");
        }

        metadata.Responses.Add(attribute.StatusCode, response);
    }

    /// <summary>
    /// Applies the OpenApiResponse attribute to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="metadata">The OpenAPI metadata to update.</param>
    /// <param name="attribute">The OpenApiResponse attribute containing response details.</param>
    private void ApplyResponseAttribute(OpenAPIMetadata metadata, IOpenApiResponseAttribute attribute)
    {
        metadata.Responses ??= [];
        var response = metadata.Responses.TryGetValue(attribute.StatusCode, out var value) ? value as OpenApiResponse : new OpenApiResponse();

        if (CreateResponseFromAttribute(attribute, response))
        {
            _ = metadata.Responses.TryAdd(attribute.StatusCode, response);
        }
    }

    /// <summary>
    /// Applies the OpenApiProperty attribute to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="metadata">The OpenAPI metadata to update.</param>
    /// <param name="attribute">The OpenApiProperty attribute containing property details.</param>
    /// <exception cref="InvalidOperationException"></exception>
    private static void ApplyPropertyAttribute(OpenAPIMetadata metadata, OpenApiPropertyAttribute attribute)
    {
        if (attribute.StatusCode is null)
        {
            return;
        }

        if (metadata.Responses is null || !metadata.Responses.TryGetValue(attribute.StatusCode, out var res))
        {
            throw new InvalidOperationException($"Response for status code '{attribute.StatusCode}' is not defined for this operation.");
        }

        if (res is OpenApiResponseReference)
        {
            throw new InvalidOperationException($"Cannot apply OpenApiPropertyAttribute to response '{attribute.StatusCode}' because it is a reference. Use inline OpenApiResponseAttribute instead.");
        }

        if (res is OpenApiResponse response)
        {
            if (response.Content is null || response.Content.Count == 0)
            {
                throw new InvalidOperationException($"Cannot apply OpenApiPropertyAttribute to response '{attribute.StatusCode}' because it has no content defined. Ensure that the response has at least one content type defined.");
            }

            foreach (var content in response.Content.Values)
            {
                if (content.Schema is null)
                {
                    throw new InvalidOperationException($"Cannot apply OpenApiPropertyAttribute to response '{attribute.StatusCode}' because its content has no schema defined.");
                }

                ApplySchemaAttr(attribute, content.Schema);
            }
        }
    }

    private void ApplyAuthorizationAttribute(MapRouteOptions routeOptions, OpenAPIMetadata metadata, OpenApiAuthorizationAttribute attribute)
    {
        metadata.SecuritySchemes ??= [];
        var policyList = BuildPolicyList(attribute.Policies);
        var securitySchemeList = Host.AddSecurityRequirementObject(attribute.Scheme, policyList, metadata.SecuritySchemes);
        routeOptions.AddSecurityRequirementObject(schemes: securitySchemeList, policies: policyList);
    }

    private static List<string> BuildPolicyList(string? policies)
    {
        return [.. (string.IsNullOrWhiteSpace(policies) ? new List<string>() : [.. policies.Split(',')])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())];
    }

    /// <summary>
    /// Processes the parameters of the specified function, applying OpenAPI annotations as needed.
    /// </summary>
    /// <param name="func">The function information.</param>
    /// <param name="help">The comment help information.</param>
    /// <param name="routeOptions">The route options to update.</param>
    /// <param name="openApiMetadata">The OpenAPI metadata to update.</param>
    /// <exception cref="InvalidOperationException">Thrown when an invalid operation occurs during parameter processing.</exception>
    private void ProcessParameters(
        FunctionInfo func,
        CommentHelpInfo help,
        MapRouteOptions routeOptions,
        OpenAPIMetadata openApiMetadata)
    {
        foreach (var paramInfo in func.Parameters.Values)
        {
            foreach (var attribute in paramInfo.Attributes)
            {
                switch (attribute)
                {
                    case OpenApiParameterAttribute paramAttr:
                        ApplyParameterAttribute(func, help, routeOptions, openApiMetadata, paramInfo, paramAttr);
                        break;
                    case OpenApiParameterRefAttribute paramRefAttr:
                        ApplyParameterRefAttribute(help, routeOptions, openApiMetadata, paramInfo, paramRefAttr);
                        break;
                    case OpenApiRequestBodyRefAttribute requestBodyRefAttr:
                        ApplyRequestBodyRefAttribute(help, routeOptions, openApiMetadata, paramInfo, requestBodyRefAttr);
                        break;
                    case OpenApiRequestBodyAttribute requestBodyAttr:
                        ApplyRequestBodyAttribute(help, routeOptions, openApiMetadata, paramInfo, requestBodyAttr);
                        break;
                    case OpenApiRequestBodyExampleRefAttribute requestBodyExampleRefAttr:
                        //ApplyRequestBodyExampleRefAttribute(openApiMetadata, paramInfo, requestBodyExampleRefAttr);
                        break;
                    case KestrunAnnotation ka:
                        throw new InvalidOperationException($"Unhandled Kestrun annotation: {ka.GetType().Name}");
                }
            }
        }
    }

    /// <summary>
    /// Applies the OpenApiParameter attribute to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="func">The function information.</param>
    /// <param name="help">The comment help information.</param>
    /// <param name="routeOptions">The route options to update.</param>
    /// <param name="metadata">The OpenAPI metadata to update.</param>
    /// <param name="paramInfo">The parameter information.</param>
    /// <param name="attribute">The OpenApiParameter attribute containing parameter details.</param>
    private void ApplyParameterAttribute(
        FunctionInfo func,
        CommentHelpInfo help,
        MapRouteOptions routeOptions,
        OpenAPIMetadata metadata,
        ParameterMetadata paramInfo,
        OpenApiParameterAttribute attribute)
    {
        metadata.Parameters ??= [];
        var parameter = new OpenApiParameter();
        if (!CreateParameterFromAttribute(attribute, parameter))
        {
            Host.Logger.Error("Error processing OpenApiParameter attribute on parameter '{paramName}' of function '{funcName}'", paramInfo.Name, func.Name);
            return;
        }

        if (!string.IsNullOrEmpty(parameter.Name) && parameter.Name != paramInfo.Name)
        {
            throw new InvalidOperationException($"Parameter name {parameter.Name} is different from variable name: '{paramInfo.Name}'.");
        }

        parameter.Name = paramInfo.Name;
        parameter.Schema = InferPrimitiveSchema(paramInfo.ParameterType);

        if (parameter.Schema is OpenApiSchema schema)
        {
            var defaultValue = func.GetDefaultParameterValue(paramInfo.Name);
            if (defaultValue is not null)
            {
                schema.Default = ToNode(defaultValue);
            }
        }

        parameter.Description ??= help.GetParameterDescription(paramInfo.Name);

        foreach (var attr in paramInfo.Attributes.OfType<CmdletMetadataAttribute>())
        {
            PowerShellAttributes.ApplyPowerShellAttribute(attr, (OpenApiSchema)parameter.Schema);
        }

        metadata.Parameters.Add(parameter);
        routeOptions.ScriptCode.Parameters.Add(new ParameterForInjectionInfo(paramInfo, parameter));
    }

    /// <summary>
    /// Applies the OpenApiParameterRef attribute to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="help">The comment help information.</param>
    /// <param name="routeOptions">The route options to update.</param>
    /// <param name="metadata">The OpenAPI metadata to update.</param>
    /// <param name="paramInfo">The parameter information.</param>
    /// <param name="attribute">The OpenApiParameterRef attribute containing parameter reference details.</param>
    /// <exception cref="InvalidOperationException">If the parameter name does not match the reference name when inlining.</exception>
    private void ApplyParameterRefAttribute(
        CommentHelpInfo help,
        MapRouteOptions routeOptions,
        OpenAPIMetadata metadata,
        ParameterMetadata paramInfo,
        OpenApiParameterRefAttribute attribute)
    {
        metadata.Parameters ??= [];
        routeOptions.ScriptCode.Parameters ??= [];

        var componentParameter = GetParameter(attribute.ReferenceId);
        IOpenApiParameter parameter;

        if (attribute.Inline)
        {
            parameter = componentParameter.Clone();
            if (componentParameter.Name != paramInfo.Name)
            {
                throw new InvalidOperationException($"Parameter name {componentParameter.Name} is different from variable name: '{paramInfo.Name}'.");
            }

            parameter.Description ??= help.GetParameterDescription(paramInfo.Name);
        }
        else
        {
            parameter = new OpenApiParameterReference(attribute.ReferenceId);
        }

        routeOptions.ScriptCode.Parameters.Add(new ParameterForInjectionInfo(paramInfo, componentParameter));
        metadata.Parameters.Add(parameter);
    }

    /// <summary>
    /// Applies the OpenApiRequestBodyRef attribute to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="help">The comment help information.</param>
    /// <param name="routeOptions">The route options to update.</param>
    /// <param name="metadata">The OpenAPI metadata to update.</param>
    /// <param name="paramInfo">The parameter information.</param>
    /// <param name="attribute">The OpenApiRequestBodyRef attribute containing request body reference details.</param>
    private void ApplyRequestBodyRefAttribute(
        CommentHelpInfo help,
        MapRouteOptions routeOptions,
        OpenAPIMetadata metadata,
        ParameterMetadata paramInfo,
        OpenApiRequestBodyRefAttribute attribute)
    {
        var referenceId = ResolveRequestBodyReferenceId(attribute, paramInfo);
        var componentRequestBody = GetRequestBody(referenceId);

        metadata.RequestBody = attribute.Inline ? componentRequestBody.Clone() : new OpenApiRequestBodyReference(referenceId);
        metadata.RequestBody.Description = attribute.Description ?? help.GetParameterDescription(paramInfo.Name);

        routeOptions.ScriptCode.Parameters.Add(new ParameterForInjectionInfo(paramInfo, componentRequestBody));
    }

    /// <summary>
    /// Resolves the reference ID for the OpenApiRequestBodyRef attribute.
    /// </summary>
    /// <param name="attribute">The OpenApiRequestBodyRef attribute.</param>
    /// <param name="paramInfo">The parameter metadata.</param>
    /// <returns>The resolved reference ID.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the ReferenceId is not specified and the parameter type is 'object',
    /// or when the ReferenceId does not match the parameter type name.
    /// </exception>
    private static string ResolveRequestBodyReferenceId(OpenApiRequestBodyRefAttribute attribute, ParameterMetadata paramInfo)
    {
        if (string.IsNullOrWhiteSpace(attribute.ReferenceId))
        {
            if (paramInfo.ParameterType.Name is "Object" or null)
            {
                throw new InvalidOperationException("OpenApiRequestBodyRefAttribute must have a ReferenceId specified when the parameter type is 'object'.");
            }

            attribute.ReferenceId = paramInfo.ParameterType.Name;
        }
        else if (paramInfo.ParameterType.Name != "Object" && attribute.ReferenceId != paramInfo.ParameterType.Name)
        {
            throw new InvalidOperationException($"ReferenceId '{attribute.ReferenceId}' is different from parameter type name '{paramInfo.ParameterType.Name}'.");
        }

        return attribute.ReferenceId;
    }

    /// <summary>
    /// Applies the OpenApiRequestBody attribute to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="help">The comment help information.</param>
    /// <param name="routeOptions">The route options to update.</param>
    /// <param name="metadata">The OpenAPI metadata to update.</param>
    /// <param name="paramInfo">The parameter information.</param>
    /// <param name="attribute">The OpenApiRequestBody attribute containing request body details.</param>
    private void ApplyRequestBodyAttribute(
        CommentHelpInfo help,
        MapRouteOptions routeOptions,
        OpenAPIMetadata metadata,
        ParameterMetadata paramInfo,
        OpenApiRequestBodyAttribute attribute)
    {
        var requestBodyPreferred = ComponentRequestBodiesExists(paramInfo.ParameterType.Name);

        if (requestBodyPreferred)
        {
            ApplyPreferredRequestBody(help, routeOptions, metadata, paramInfo, attribute);
            return;
        }

        var requestBody = new OpenApiRequestBody();
        var schema = InferPrimitiveSchema(type: paramInfo.ParameterType, inline: attribute.Inline);

        if (!CreateRequestBodyFromAttribute(attribute, requestBody, schema))
        {
            return;
        }

        metadata.RequestBody = requestBody;
        metadata.RequestBody.Description ??= help.GetParameterDescription(paramInfo.Name);
        routeOptions.ScriptCode.Parameters.Add(new ParameterForInjectionInfo(paramInfo, requestBody));
    }

    /// <summary>
    /// Applies the preferred request body from components to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="help">The comment help information.</param>
    /// <param name="routeOptions">The route options to update.</param>
    /// <param name="metadata">The OpenAPI metadata to update.</param>
    /// <param name="paramInfo">The parameter information.</param>
    /// <param name="attribute">The OpenApiRequestBody attribute containing request body details.</param>
    private void ApplyPreferredRequestBody(
        CommentHelpInfo help,
        MapRouteOptions routeOptions,
        OpenAPIMetadata metadata,
        ParameterMetadata paramInfo,
        OpenApiRequestBodyAttribute attribute)
    {
        var componentRequestBody = GetRequestBody(paramInfo.ParameterType.Name);

        metadata.RequestBody = attribute.Inline
            ? componentRequestBody.Clone()
            : new OpenApiRequestBodyReference(paramInfo.ParameterType.Name);

        metadata.RequestBody.Description ??= help.GetParameterDescription(paramInfo.Name);
        routeOptions.ScriptCode.Parameters.Add(new ParameterForInjectionInfo(paramInfo, componentRequestBody));
    }

    /// <summary>
    /// Ensures that the OpenAPIMetadata has default responses defined.
    /// </summary>
    /// <param name="metadata">The OpenAPI metadata to update.</param>
    private static void EnsureDefaultResponses(OpenAPIMetadata metadata)
    {
        metadata.Responses ??= [];
        if (metadata.Responses.Count > 0)
        {
            return;
        }

        metadata.Responses.Add("200", new OpenApiResponse { Description = "Ok" });
        metadata.Responses.Add("default", new OpenApiResponse { Description = "Unexpected error" });
    }

    /// <summary>
    /// Finalizes the route options by adding OpenAPI metadata and configuring defaults.
    /// </summary>
    /// <param name="func">The function information.</param>
    /// <param name="sb">The script block.</param>
    /// <param name="metadata">The OpenAPI metadata.</param>
    /// <param name="routeOptions">The route options to update.</param>
    /// <param name="parsedVerb">The HTTP verb parsed from the function.</param>
    private void FinalizeRouteOptions(
        FunctionInfo func,
        ScriptBlock sb,
        OpenAPIMetadata metadata,
        MapRouteOptions routeOptions,
        HttpVerb parsedVerb)
    {
        routeOptions.OpenAPI.Add(parsedVerb, metadata);

        if (string.IsNullOrWhiteSpace(routeOptions.Pattern))
        {
            routeOptions.Pattern = "/" + func.Name;
        }

        if (!string.IsNullOrWhiteSpace(metadata.CorsPolicy))
        {
            routeOptions.CorsPolicy = metadata.CorsPolicy;
        }

        routeOptions.ScriptCode.ScriptBlock = sb;
        routeOptions.DefaultResponseContentType = "application/json";
        _ = Host.AddMapRoute(routeOptions);
    }

    /// <summary>
    /// Creates a request body from the given attribute.
    /// </summary>
    /// <param name="attribute">The attribute containing request body information.</param>
    /// <param name="requestBody">The OpenApiRequestBody object to populate.</param>
    /// <param name="schema">The schema to associate with the request body.</param>
    /// <returns>True if the request body was created successfully; otherwise, false.</returns>
    private static bool CreateRequestBodyFromAttribute(KestrunAnnotation attribute, OpenApiRequestBody requestBody, IOpenApiSchema schema)
    {
        switch (attribute)
        {
            case OpenApiRequestBodyAttribute request:
                requestBody.Description = request.Description;
                requestBody.Required = request.Required;
                // Content
                requestBody.Content ??= new Dictionary<string, IOpenApiMediaType>(StringComparer.Ordinal);
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
}
