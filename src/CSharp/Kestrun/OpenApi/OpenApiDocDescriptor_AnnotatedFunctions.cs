using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Management.Automation.Language;
using System.Text.Json.Nodes;
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
        var callbacks = cmdInfos
                .Where(f => f.ScriptBlock.Attributes?.All(a => a is OpenApiCallbackAttribute) != false);

        var others = cmdInfos
            .Where(f => f.ScriptBlock.Attributes?.All(a => a is not OpenApiCallbackAttribute) != false);
        // (equivalent to NOT having any callback attribute)

        foreach (var func in callbacks)
        {
            ProcessFunction(func);
        }

        BuildCallbacks(Callbacks);
        foreach (var func in others)
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
            // Create route options and OpenAPI metadata
            var routeOptions = new MapRouteOptions();
            var openApiMetadata = new OpenAPIPathMetadata(mapOptions: routeOptions);
            // Process attributes to populate route options and OpenAPI metadata
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
        OpenAPIPathMetadata openApiMetadata)
    {
        var parsedVerb = HttpVerb.Get;

        foreach (var attr in attrs)
        {
            try
            {
                switch (attr)
                {
                    case OpenApiPathAttribute path:
                        parsedVerb = ApplyPathAttribute(func, help, routeOptions, openApiMetadata, parsedVerb, path);
                        break;
                    case OpenApiWebhookAttribute webhook:
                        parsedVerb = ApplyPathAttribute(func, help, routeOptions, openApiMetadata, parsedVerb, webhook);
                        break;
                    case OpenApiCallbackAttribute callbackOperation:
                        parsedVerb = ApplyPathAttribute(func, help, routeOptions, openApiMetadata, parsedVerb, callbackOperation);
                        break;
                    case OpenApiExtensionAttribute extensionAttr:
                        ApplyExtensionAttribute(openApiMetadata, extensionAttr);
                        break;
                    case OpenApiResponseRefAttribute responseRef:
                        ApplyResponseRefAttribute(openApiMetadata, responseRef);
                        break;
                    case OpenApiResponseAttribute responseAttr:
                        ApplyResponseAttribute(openApiMetadata, responseAttr, routeOptions);
                        break;
                    case OpenApiResponseExampleRefAttribute responseAttr:
                        ApplyResponseAttribute(openApiMetadata, responseAttr, routeOptions);
                        break;
                    case OpenApiResponseLinkRefAttribute linkRefAttr:
                        ApplyResponseLinkAttribute(openApiMetadata, linkRefAttr);
                        break;
                    case OpenApiPropertyAttribute propertyAttr:
                        ApplyPropertyAttribute(openApiMetadata, propertyAttr);
                        break;
                    case OpenApiAuthorizationAttribute authAttr:
                        ApplyAuthorizationAttribute(routeOptions, openApiMetadata, authAttr);
                        break;
                    case IOpenApiResponseHeaderAttribute responseHeaderAttr:
                        ApplyResponseHeaderAttribute(openApiMetadata, responseHeaderAttr);
                        break;
                    case OpenApiCallbackRefAttribute callbackRefAttr:
                        ApplyCallbackRefAttribute(openApiMetadata, callbackRefAttr);
                        break;
                    case KestrunAnnotation ka:
                        throw new InvalidOperationException($"Unhandled Kestrun annotation: {ka.GetType().Name}");
                }
            }
            catch (InvalidOperationException ex)
            {
                Host.Logger.Error(ex, "Error processing OpenApiPath attribute on function '{funcName}': {message}", func.Name, ex.Message);
            }
            catch (Exception ex)
            {
                Host.Logger.Error(ex, "Error processing OpenApiPath attribute on function '{funcName}': {message}", func.Name, ex.Message);
            }
        }

        return parsedVerb;
    }

    /// <summary>
    /// Applies the OpenApiExtension attribute to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="openApiMetadata">The OpenAPI metadata to which the extension will be applied.</param>
    /// <param name="extensionAttr">The OpenApiExtension attribute containing the extension data.</param>
    /// <exception cref="InvalidOperationException">Thrown when the extension JSON is invalid or cannot be parsed.</exception>
    private void ApplyExtensionAttribute(OpenAPIPathMetadata openApiMetadata, OpenApiExtensionAttribute extensionAttr)
    {
        if (Host.Logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
        {
            Host.Logger.Debug("Applying OpenApiExtension '{extensionName}' to function metadata", extensionAttr.Name);
        }
        openApiMetadata.Extensions ??= [];

        // Parse string into a JsonNode tree.
        var node = JsonNode.Parse(extensionAttr.Json);
        if (node is null)
        {
            Host.Logger.Error("Error parsing OpenAPI extension '{extensionName}': JSON is null", extensionAttr.Name);
            return;
        }
        openApiMetadata.Extensions[extensionAttr.Name] = new JsonNodeExtension(node);
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
        OpenAPIPathMetadata metadata,
        HttpVerb parsedVerb,
        IOpenApiPathAttribute oaPath)
    {
        var httpVerb = oaPath.HttpVerb ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(httpVerb))
        {
            parsedVerb = HttpVerbExtensions.FromMethodString(httpVerb);
            routeOptions.HttpVerbs.Add(parsedVerb);
        }

        var pattern = oaPath.Pattern;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new InvalidOperationException("OpenApiPath attribute must specify a non-empty Pattern property.");
        }
        // Apply pattern, summary, description, tags
        routeOptions.Pattern = pattern;
        metadata.Summary = ChooseFirstNonEmpty(oaPath.Summary, help.GetSynopsis());
        metadata.Description = ChooseFirstNonEmpty(oaPath.Description, help.GetDescription());
        metadata.Tags = [.. oaPath.Tags];

        // Apply deprecated flag if specified
        metadata.Deprecated |= oaPath.Deprecated;
        // Apply document ID if specified
        metadata.DocumentId = oaPath.DocumentId;
        switch (oaPath)
        {
            case OpenApiPathAttribute oaPathConcrete:
                ApplyPathLikePath(func, routeOptions, metadata, oaPathConcrete, pattern);
                break;
            case OpenApiWebhookAttribute oaWebhook:
                ApplyPathLikeWebhook(func, metadata, oaWebhook, pattern);
                break;
            case OpenApiCallbackAttribute oaCallback:
                ApplyPathLikeCallback(func, metadata, oaCallback, httpVerb, pattern);
                break;
        }

        return parsedVerb;
    }

    /// <summary>
    /// Applies the OpenApiPath attribute to the function's route options and metadata for a standard path.
    /// </summary>
    /// <param name="func">The function information.</param>
    /// <param name="routeOptions">The route options to configure.</param>
    /// <param name="metadata">The OpenAPI metadata to populate.</param>
    /// <param name="oaPath">The OpenApiPath attribute instance.</param>
    /// <param name="pattern">The route pattern.</param>
    private static void ApplyPathLikePath(
        FunctionInfo func,
        MapRouteOptions routeOptions,
        OpenAPIPathMetadata metadata,
        OpenApiPathAttribute oaPath,
        string pattern)
    {
        metadata.Pattern = pattern;
        metadata.PathLikeKind = OpenApiPathLikeKind.Path;
        if (!string.IsNullOrWhiteSpace(oaPath.CorsPolicy))
        {
            // Apply Cors policy name if specified
            routeOptions.CorsPolicy = oaPath.CorsPolicy;
        }

        metadata.OperationId = oaPath.OperationId is null
            ? func.Name
            : string.IsNullOrWhiteSpace(oaPath.OperationId) ? metadata.OperationId : oaPath.OperationId;
    }
    /// <summary>
    /// Applies the OpenApiWebhook attribute to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="func">The function information.</param>
    /// <param name="metadata">The OpenAPI metadata to populate.</param>
    /// <param name="oaPath">The OpenApiWebhook attribute instance.</param>
    /// <param name="pattern">The route pattern.</param>
    private static void ApplyPathLikeWebhook(FunctionInfo func, OpenAPIPathMetadata metadata, OpenApiWebhookAttribute oaPath, string pattern)
    {
        metadata.Pattern = pattern;
        metadata.PathLikeKind = OpenApiPathLikeKind.Webhook;
        metadata.OperationId = oaPath.OperationId is null
            ? func.Name
            : string.IsNullOrWhiteSpace(oaPath.OperationId) ? metadata.OperationId : oaPath.OperationId;
    }

    /// <summary>
    /// Applies the OpenApiCallback attribute to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="func">The function information.</param>
    /// <param name="metadata">The OpenAPI metadata to populate.</param>
    /// <param name="oaCallback">The OpenApiCallback attribute instance.</param>
    /// <param name="httpVerb">The HTTP verb associated with the callback.</param>
    /// <param name="callbackPattern">The callback route pattern.</param>
    /// <exception cref="InvalidOperationException">Thrown when the Expression property of the OpenApiCallback attribute is null or whitespace.</exception>
    private static void ApplyPathLikeCallback(
        FunctionInfo func,
        OpenAPIPathMetadata metadata,
        OpenApiCallbackAttribute oaCallback,
        string httpVerb,
        string callbackPattern)
    {
        // Callbacks are neither paths nor webhooks
        metadata.PathLikeKind = OpenApiPathLikeKind.Callback;
        if (string.IsNullOrWhiteSpace(oaCallback.Expression))
        {
            throw new InvalidOperationException("OpenApiCallback attribute must specify a non-empty Expression property.");
        }
        // Callbacks must have an expression
        metadata.Expression = CallbackOperationId.BuildCallbackKey(oaCallback.Expression, callbackPattern);
        metadata.Inline = oaCallback.Inline;
        metadata.Pattern = func.Name;
        metadata.OperationId = string.IsNullOrWhiteSpace(oaCallback.OperationId)
           ? CallbackOperationId.FromLastSegment(func.Name, httpVerb, oaCallback.Expression)
           : oaCallback.OperationId;
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

    /// <summary>
    /// Applies the OpenApiResponseRef attribute to the function's OpenAPI metadata.
    /// </summary>
    ///     <param name="metadata">The OpenAPI metadata to update.</param>
    /// <param name="attribute">The OpenApiResponseRef attribute containing response reference details.</param>
    private void ApplyResponseRefAttribute(OpenAPIPathMetadata metadata, OpenApiResponseRefAttribute attribute)
    {
        metadata.Responses ??= [];

        if (!TryGetResponseItem(attribute.ReferenceId, out var response, out var inline))
        {
            throw new InvalidOperationException($"Response component with ID '{attribute.ReferenceId}' not found.");
        }

        IOpenApiResponse iResponse = attribute.Inline || inline ? response!.Clone() : new OpenApiResponseReference(attribute.ReferenceId);

        if (attribute.Description is not null)
        {
            iResponse.Description = attribute.Description;
        }

        if (metadata.Responses.ContainsKey(attribute.StatusCode))
        {
            throw new InvalidOperationException($"Response for status code '{attribute.StatusCode}' is already defined for this operation.");
        }

        metadata.Responses.Add(attribute.StatusCode, iResponse);
    }

    /// <summary>
    /// Applies the OpenApiResponse attribute to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="metadata">The OpenAPI metadata to update.</param>
    /// <param name="attribute">The OpenApiResponse attribute containing response details.</param>
    /// <param name="routeOptions">The route options to update.</param>
    private void ApplyResponseAttribute(OpenAPIPathMetadata metadata, IOpenApiResponseAttribute attribute, MapRouteOptions routeOptions)
    {
        metadata.Responses ??= [];
        var response = metadata.Responses.TryGetValue(attribute.StatusCode, out var value) ? value as OpenApiResponse : new OpenApiResponse();
        if (response is not null && CreateResponseFromAttribute(attribute, response))
        {
            _ = metadata.Responses.TryAdd(attribute.StatusCode, response);
            if (routeOptions.DefaultResponseContentType is null)
            {
                var defaultStatusCode = SelectDefaultSuccessResponse(metadata.Responses);
                if (defaultStatusCode is not null && metadata.Responses.TryGetValue(defaultStatusCode, out var defaultResponse) &&
                    defaultResponse.Content is not null && defaultResponse.Content.Count > 0)
                {
                    routeOptions.DefaultResponseContentType =
                        defaultResponse.Content.Keys.First();
                }
            }
        }
    }

    /// <summary>
    /// Selects the default success response (2xx) from the given OpenApiResponses.
    /// </summary>
    /// <param name="responses">The collection of OpenApiResponses to select from.</param>
    /// <returns>The status code of the default success response, or null if none found.</returns>
    private static string? SelectDefaultSuccessResponse(OpenApiResponses responses)
    {
        return responses
            .Select(kvp => new
            {
                StatusCode = kvp.Key,
                Code = TryParseStatusCode(kvp.Key),
                Response = kvp.Value
            })
            .Where(x =>
                x.Code is >= 200 and < 300 &&
                HasContent(x.Response))
            .OrderBy(x => x.Code)
            .Select(x => x.StatusCode)
            .FirstOrDefault();
    }

    /// <summary>
    /// Tries to parse the given status code string into an integer.
    /// </summary>
    /// <param name="statusCode">The status code as a string.</param>
    /// <returns>The parsed integer status code, or -1 if parsing fails.</returns>
    private static int TryParseStatusCode(string statusCode)
        => int.TryParse(statusCode, out var code) ? code : -1;

    /// <summary>
    /// Determines if the given response has content defined.
    /// </summary>
    /// <param name="response">The OpenAPI response to check for content.</param>
    /// <returns>True if the response has content; otherwise, false.</returns>
    private static bool HasContent(IOpenApiResponse response)
    {
        // If your concrete type is OpenApiResponse (common), this is the easiest path:
        if (response is OpenApiResponse r)
        {
            return r.Content is not null && r.Content.Count > 0;
        }

        // Otherwise, we can't reliably know. Be conservative:
        return false;
    }

    /// <summary>
    /// Applies the OpenApiProperty attribute to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="metadata">The OpenAPI metadata to update.</param>
    /// <param name="attribute">The OpenApiProperty attribute containing property details.</param>
    /// <exception cref="InvalidOperationException"></exception>
    private static void ApplyPropertyAttribute(OpenAPIPathMetadata metadata, OpenApiPropertyAttribute attribute)
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

    private void ApplyAuthorizationAttribute(MapRouteOptions routeOptions, OpenAPIPathMetadata metadata, OpenApiAuthorizationAttribute attribute)
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
        OpenAPIPathMetadata openApiMetadata)
    {
        foreach (var paramInfo in func.Parameters.Values)
        {
            // First pass for parameter and request body attributes
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
                    case OpenApiRequestBodyExampleRefAttribute:
                    case OpenApiParameterExampleRefAttribute:
                        // Do nothing here; handled later
                        break;
                    case KestrunAnnotation ka:
                        throw new InvalidOperationException($"Unhandled Kestrun annotation: {ka.GetType().Name}");
                }
            }
            // Second pass for example references
            foreach (var attribute in paramInfo.Attributes)
            {
                switch (attribute)
                {
                    case OpenApiParameterAttribute:
                    case OpenApiParameterRefAttribute:
                    case OpenApiRequestBodyRefAttribute:
                    case OpenApiRequestBodyAttribute:
                        // Already handled
                        break;
                    case OpenApiRequestBodyExampleRefAttribute requestBodyExampleRefAttr:
                        ApplyRequestBodyExampleRefAttribute(openApiMetadata, requestBodyExampleRefAttr);
                        break;
                    case OpenApiParameterExampleRefAttribute parameterExampleRefAttr:
                        ApplyParameterExampleRefAttribute(openApiMetadata, paramInfo, parameterExampleRefAttr);
                        break;
                    case KestrunAnnotation ka:
                        throw new InvalidOperationException($"Unhandled Kestrun annotation: {ka.GetType().Name}");
                }
            }
        }
    }

    #region Parameter Handlers
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
        OpenAPIPathMetadata metadata,
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
                schema.Default = OpenApiJsonNodeFactory.ToNode(defaultValue);
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
        OpenAPIPathMetadata metadata,
        ParameterMetadata paramInfo,
        OpenApiParameterRefAttribute attribute)
    {
        metadata.Parameters ??= [];
        routeOptions.ScriptCode.Parameters ??= [];

        if (!TryGetParameterItem(attribute.ReferenceId, out var componentParameter, out var isInline) ||
             componentParameter is null)
        {
            throw new InvalidOperationException($"Parameter component with ID '{attribute.ReferenceId}' not found.");
        }
        IOpenApiParameter parameter;

        if (attribute.Inline || isInline)
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

    private void ApplyParameterExampleRefAttribute(
       OpenAPIPathMetadata metadata,
       ParameterMetadata paramInfo,
       OpenApiParameterExampleRefAttribute attribute)
    {
        var parameters = metadata.Parameters
        ?? throw new InvalidOperationException(
            "OpenApiParameterExampleRefAttribute must follow OpenApiParameterAttribute or OpenApiParameterRefAttribute.");

        var parameter = parameters.FirstOrDefault(p => p.Name == paramInfo.Name)
        ?? throw new InvalidOperationException(
            $"OpenApiParameterExampleRefAttribute requires the parameter '{paramInfo.Name}' to be defined.");
        if (parameter is OpenApiParameterReference)
        {
            throw new InvalidOperationException(
                "Cannot apply OpenApiParameterExampleRefAttribute to a parameter reference.");
        }
        if (parameter is OpenApiParameter opp)
        {
            opp.Examples ??= new Dictionary<string, IOpenApiExample>();
            // Clone or reference the example
            _ = TryAddExample(opp.Examples, attribute);
        }
    }

    #endregion
    #region Request Body Handlers
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
        OpenAPIPathMetadata metadata,
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
    private string ResolveRequestBodyReferenceId(OpenApiRequestBodyRefAttribute attribute, ParameterMetadata paramInfo)
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
            return FindReferenceIdForParameter(attribute.ReferenceId, paramInfo);
        }
        // Return the reference ID as is
        return attribute.ReferenceId;
    }

    /// <summary>
    /// Finds and validates the reference ID for a request body parameter.
    /// </summary>
    /// <param name="referenceId">The reference ID to validate.</param>
    /// <param name="paramInfo">The parameter metadata.</param>
    /// <returns>The validated reference ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the reference ID does not match the parameter type name.</exception>
    private string FindReferenceIdForParameter(string referenceId, ParameterMetadata paramInfo)
    {
        // Ensure the reference ID exists and has a schema
        if (!TryGetFirstRequestBodySchema(referenceId, out var schema))
        {
            throw new InvalidOperationException(
                $"Request body component with ReferenceId '{referenceId}' was not found or does not define a schema.");
        }
        // Validate that the schema matches the parameter type
        if (!IsRequestBodySchemaMatchForParameter(schema, paramInfo.ParameterType))
        {
            throw new InvalidOperationException(
                $"Schema for request body component '{referenceId}' does not match parameter type '{paramInfo.ParameterType.Name}'.");
        }
        // return the validated reference ID
        return referenceId;
    }

    /// <summary>
    /// Attempts to retrieve the first schema defined on a request body component.
    /// </summary>
    /// <param name="referenceId">The request body component reference ID.</param>
    /// <param name="schema">The extracted schema when available.</param>
    /// <returns><see langword="true"/> if a non-null schema is found; otherwise <see langword="false"/>.</returns>
    private bool TryGetFirstRequestBodySchema(string referenceId, out IOpenApiSchema schema)
    {
        schema = null!;

        if (!TryGetRequestBodyItem(referenceId, out var requestBody, out _))
        {
            return false;
        }

        if (requestBody?.Content is null || requestBody.Content.Count == 0)
        {
            return false;
        }

        schema = requestBody.Content.First().Value.Schema!;
        return schema is not null;
    }

    /// <summary>
    /// Determines whether a request-body schema matches a given CLR parameter type.
    /// </summary>
    /// <param name="schema">The schema declared for the request body.</param>
    /// <param name="parameterType">The CLR parameter type being validated.</param>
    /// <returns><see langword="true"/> if the schema matches the parameter type; otherwise <see langword="false"/>.</returns>
    private static bool IsRequestBodySchemaMatchForParameter(IOpenApiSchema schema, Type parameterType)
    {
        if (schema is OpenApiSchemaReference schemaRef)
        {
            return schemaRef.Reference.Id == parameterType.Name;
        }

        if (schema is OpenApiSchema inlineSchema && PrimitiveSchemaMap.TryGetValue(parameterType, out var valueType))
        {
            var expected = valueType();
            return inlineSchema.Format == expected.Format && inlineSchema.Type == expected.Type;
        }

        return false;
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
        OpenAPIPathMetadata metadata,
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
    /// Applies the OpenApiRequestBodyExampleRef attribute to the function's OpenAPI metadata.
    /// </summary>
    /// <param name="metadata">The OpenAPI metadata to update.</param>
    /// <param name="attribute">The OpenApiRequestBodyExampleRef attribute containing example reference details.</param>
    /// <exception cref="InvalidOperationException">Thrown when the request body or its content is not properly defined.</exception>
    private void ApplyRequestBodyExampleRefAttribute(
       OpenAPIPathMetadata metadata,
       OpenApiRequestBodyExampleRefAttribute attribute)
    {
        var requestBody = metadata.RequestBody
        ?? throw new InvalidOperationException(
            "OpenApiRequestBodyExampleRefAttribute must follow OpenApiRequestBodyAttribute or OpenApiRequestBodyRefAttribute.");

        if (requestBody.Content is null)
        {
            throw new InvalidOperationException(
                "OpenApiRequestBodyExampleRefAttribute requires the request body to have content defined.");
        }

        foreach (var oamt in requestBody.Content.Values.OfType<OpenApiMediaType>())
        {
            oamt.Examples ??= new Dictionary<string, IOpenApiExample>();
            _ = TryAddExample(oamt.Examples, attribute);
        }
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
        OpenAPIPathMetadata metadata,
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
    #endregion

    /// <summary>
    /// Ensures that the OpenAPIPathMetadata has default responses defined.
    /// </summary>
    /// <param name="metadata">The OpenAPI metadata to update.</param>
    private static void EnsureDefaultResponses(OpenAPIPathMetadata metadata)
    {
        metadata.Responses ??= [];
        if (metadata.Responses.Count > 0)
        {
            return;
        }
        if (metadata.IsOpenApiCallback)
        {
            metadata.Responses.Add("204", new OpenApiResponse { Description = "Accepted" });
        }
        else
        {
            metadata.Responses.Add("200", new OpenApiResponse { Description = "Ok" });
            metadata.Responses.Add("default", new OpenApiResponse { Description = "Unexpected error" });
        }
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
        OpenAPIPathMetadata metadata,
        MapRouteOptions routeOptions,
        HttpVerb parsedVerb)
    {
        metadata.DocumentId ??= Host.OpenApiDocumentIds;
        var documentIds = metadata.DocumentId;
        if (metadata.IsOpenApiPath)
        {
            FinalizePathRouteOptions(func, sb, metadata, routeOptions, parsedVerb);
            return;
        }

        if (metadata.IsOpenApiWebhook)
        {
            RegisterWebhook(func, sb, metadata, parsedVerb, documentIds);
            return;
        }

        if (metadata.IsOpenApiCallback)
        {
            RegisterCallback(func, sb, metadata, parsedVerb, documentIds);
        }
    }

    /// <summary>
    /// Finalizes the route options for a standard OpenAPI path.
    /// </summary>
    /// <param name="func">The function information.</param>
    /// <param name="sb">The script block.</param>
    /// <param name="metadata">The OpenAPI metadata.</param>
    /// <param name="routeOptions">The route options to update.</param>
    /// <param name="parsedVerb">The HTTP verb parsed from the function.</param>
    private void FinalizePathRouteOptions(
        FunctionInfo func,
        ScriptBlock sb,
        OpenAPIPathMetadata metadata,
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
    /// Registers a webhook in the OpenAPI document descriptors.
    /// </summary>
    /// <param name="func">The function information.</param>
    /// <param name="sb">The script block.</param>
    /// <param name="metadata">The OpenAPI path metadata.</param>
    /// <param name="parsedVerb">The HTTP verb parsed from the function.</param>
    /// <param name="documentIds">The collection of OpenAPI document IDs.</param>
    private void RegisterWebhook(FunctionInfo func, ScriptBlock sb, OpenAPIPathMetadata metadata, HttpVerb parsedVerb, IEnumerable<string> documentIds)
    {
        EnsureParamOnlyScriptBlock(func, sb, kind: "webhook");
        foreach (var docId in documentIds)
        {
            var docdesc = GetDocDescriptorOrThrow(docId, attributeName: "OpenApiWebhook");
            _ = docdesc.WebHook.TryAdd((metadata.Pattern, parsedVerb), metadata);
        }
    }
    /// <summary>
    /// Registers a callback in the OpenAPI document descriptors.
    /// </summary>
    /// <param name="func">The function information.</param>
    /// <param name="sb">The script block.</param>
    /// <param name="metadata">The OpenAPI path metadata.</param>
    /// <param name="parsedVerb">The HTTP verb parsed from the function.</param>
    /// <param name="documentIds">The collection of OpenAPI document IDs.</param>
    private void RegisterCallback(FunctionInfo func, ScriptBlock sb, OpenAPIPathMetadata metadata, HttpVerb parsedVerb, IEnumerable<string> documentIds)
    {
        EnsureParamOnlyScriptBlock(func, sb, kind: "callback");
        foreach (var docId in documentIds)
        {
            var docdesc = GetDocDescriptorOrThrow(docId, attributeName: "OpenApiCallback");
            _ = docdesc.Callbacks.TryAdd((metadata.Pattern, parsedVerb), metadata);
        }
    }

    /// <summary>
    /// Retrieves the OpenApiDocDescriptor for the specified document ID or throws an exception if not found.
    /// </summary>
    /// <param name="docId">The document ID to look up.</param>
    /// <param name="attributeName">The name of the attribute requesting the document.</param>
    /// <returns>The corresponding OpenApiDocDescriptor.</returns>
    private OpenApiDocDescriptor GetDocDescriptorOrThrow(string docId, string attributeName)
    {
        return Host.OpenApiDocumentDescriptor.TryGetValue(docId, out var docdesc)
            ? docdesc
            : throw new InvalidOperationException($"The OpenAPI document ID '{docId}' specified in the {attributeName} attribute does not exist in the Kestrun host.");
    }

    /// <summary>
    /// Ensures that the ScriptBlock contains only a param() block with no executable statements.
    /// </summary>
    /// <param name="func">The function information.</param>
    /// <param name="sb">The ScriptBlock to validate.</param>
    /// <param name="kind">The kind of function (e.g., "webhook" or "callback").</param>
    /// <exception cref="InvalidOperationException">Thrown if the ScriptBlock contains executable statements other than a param() block.</exception>
    private static void EnsureParamOnlyScriptBlock(FunctionInfo func, ScriptBlock sb, string kind)
    {
        if (!PsScriptBlockValidation.IsParamLast(sb))
        {
            throw new InvalidOperationException($"The ScriptBlock for {kind} function '{func.Name}' must contain only a param() block with no executable statements.");
        }
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
                    mediaType.Example = OpenApiJsonNodeFactory.ToNode(request.Example);
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
