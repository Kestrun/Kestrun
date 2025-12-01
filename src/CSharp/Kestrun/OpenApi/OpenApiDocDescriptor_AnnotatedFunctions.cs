using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Reflection;
using System.Text.Json.Nodes;
using Json.Schema;
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
            try
            {
                var help = func.GetHelp();
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
                    try
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
                            else if (!string.IsNullOrWhiteSpace(help.GetSynopsis()))
                            {
                                openApiAttr.Summary = help.GetSynopsis();
                            }

                            // Description
                            if (!string.IsNullOrWhiteSpace(oaPath.Description))
                            {
                                openApiAttr.Description = oaPath.Description;
                            }
                            else if (!string.IsNullOrWhiteSpace(help.GetDescription()))
                            {
                                openApiAttr.Description = help.GetDescription();
                            }

                            // Tags
                            openApiAttr.Tags = [.. oaPath.Tags];

                            // OperationId
                            // if not specified, default to function name
                            // if blank, do not set
                            if (oaPath.OperationId is null)
                            {
                                // Default to function name if not specified
                                openApiAttr.OperationId = func.Name;
                            }
                            else if (!string.IsNullOrWhiteSpace(oaPath.OperationId))
                            {
                                // Use specified OperationId
                                openApiAttr.OperationId = oaPath.OperationId;
                            }
                            // Deprecated flag (per-verb OpenAPI metadata)
                            openApiAttr.Deprecated |= oaPath.Deprecated; // carry forward deprecated flag
                        }
                        //TODO: handle other OpenAPI attributes like Header
                        else if (attr is OpenApiResponseRefAttribute oaRRa)
                        {
                            openApiAttr.Responses ??= [];
                            IOpenApiResponse response;
                            // Determine if we inline the referenced response or use a $ref
                            if (oaRRa.Inline)
                            {
                                var componentResponse = GetResponse(oaRRa.ReferenceId);
                                response = componentResponse.Clone();
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
                            if (openApiAttr.Responses.ContainsKey(oaRRa.StatusCode))
                            {
                                throw new InvalidOperationException($"Response for status code '{oaRRa.StatusCode}' is already defined for this operation.");
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
                        else if (attr is OpenApiPropertyAttribute oaPA)
                        {
                            if (oaPA.StatusCode is null)
                            {
                                continue;
                            }

                            if (openApiAttr.Responses is null || !openApiAttr.Responses.ContainsKey(oaPA.StatusCode))
                            {
                                throw new InvalidOperationException($"Response for status code '{oaPA.StatusCode}' is not defined for this operation.");
                            }
                            var res = openApiAttr.Responses[oaPA.StatusCode];
                            if (res is OpenApiResponseReference)
                            {
                                throw new InvalidOperationException($"Cannot apply OpenApiPropertyAttribute to response '{oaPA.StatusCode}' because it is a reference. Use inline OpenApiResponseAttribute instead.");
                            }

                            var response = (OpenApiResponse)res;
                            if (response.Content is null || response.Content.Count == 0)
                            {
                                throw new InvalidOperationException($"Cannot apply OpenApiPropertyAttribute to response '{oaPA.StatusCode}' because it has no content defined. Ensure that the response has at least one content type defined.");
                            }
                            foreach (var content in response.Content.Values)
                            {
                                if (content.Schema is null)
                                {
                                    throw new InvalidOperationException($"Cannot apply OpenApiPropertyAttribute to response '{oaPA.StatusCode}' because its content has no schema defined.");
                                }
                                ApplySchemaAttr(oaPA, content.Schema);
                            }
                        }
                        else if (attr is OpenApiAuthorizationAttribute oaRBa)
                        {
                            openApiAttr.SecuritySchemes ??= [];
                            // Parse policies
                            List<string> policyList = [.. (string.IsNullOrWhiteSpace(oaRBa.Policies) ? new List<string>() : [.. oaRBa.Policies.Split(',')])
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .Select(p => p.Trim())];

                            // Add security requirement object for this verb
                            // If no scheme provided, the schema is derived from the policies
                            var securitySchemeList = Host.AddSecurityRequirementObject(oaRBa.Scheme, policyList, openApiAttr.SecuritySchemes);
                            routeOptions.AddSecurityRequirementObject(schemes: securitySchemeList, policies: policyList);
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
                    catch (InvalidOperationException ex)
                    {
                        Host.Logger.Error("Error processing OpenApiPath attribute on function '{funcName}': {message}", func.Name, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Host.Logger.Error("Error processing OpenApiPath attribute on function '{funcName}': {message}", func.Name, ex.Message);
                    }
                }
                // Process parameters for [OpenApiParameter] attributes
                foreach (var paramInfo in func.Parameters.Values)
                {
                    // Check for [OpenApiParameter] attribute on the parameter
                    var paramAttrs = paramInfo.Attributes;
                    foreach (var pAttr in paramAttrs)
                    {
                        if (pAttr is OpenApiParameterAttribute oaParamAttr)
                        {
                            openApiAttr.Parameters ??= [];
                            var parameter = new OpenApiParameter();
                            if (CreateParameterFromAttribute(oaParamAttr, parameter))
                            {
                                parameter.Name = !string.IsNullOrEmpty(parameter.Name) && parameter.Name != paramInfo.Name
                                    ? throw new InvalidOperationException(
                                         $"Parameter name {parameter.Name} is different from variable name'{paramInfo.Name}'.")
                                    : paramInfo.Name;
                                parameter.Schema = InferPrimitiveSchema(paramInfo.ParameterType);
                                // Apply description from help if not set
                                parameter.Description ??= help.GetParameterDescription(paramInfo.Name);
                                foreach (var attr in paramInfo.Attributes.OfType<CmdletMetadataAttribute>())
                                {
                                    PowerShellAttributes.ApplyPowerShellAttributes(attr, (OpenApiSchema)parameter.Schema);
                                }
                                openApiAttr.Parameters.Add(parameter);
                                // Add to script code parameter injection info
                                routeOptions.ScriptCode.Parameters.Add(new ParameterForInjectionInfo(paramInfo.Name, parameter));
                            }
                            else
                            {
                                Host.Logger.Error("Error processing OpenApiParameter attribute on parameter '{paramName}' of function '{funcName}'", paramInfo.Name, func.Name);
                            }
                        }
                        else if (pAttr is OpenApiParameterRefAttribute oaParamRefAttr)
                        {
                            openApiAttr.Parameters ??= [];
                            routeOptions.ScriptCode.Parameters ??= [];
                            IOpenApiParameter parameter;
                            var componentParameter = GetParameter(oaParamRefAttr.ReferenceId);
                            // Determine if we inline the referenced parameter or use a $ref
                            if (oaParamRefAttr.Inline)
                            {
                                parameter = componentParameter.Clone();
                                // Apply any name override
                                if (componentParameter.Name != paramInfo.Name)
                                {
                                    throw new InvalidOperationException(
                                         $"Parameter name {componentParameter.Name} is different from variable name'{paramInfo.Name}'.");
                                }
                                // Apply description from help if not set
                                parameter.Description ??= help.GetParameterDescription(paramInfo.Name);
                            }
                            else
                            {
                                parameter = new OpenApiParameterReference(oaParamRefAttr.ReferenceId);
                            }
                            // Apply any name override
                            routeOptions.ScriptCode.Parameters.Add(new ParameterForInjectionInfo(paramInfo.Name, componentParameter));

                            openApiAttr.Parameters.Add(parameter);
                        }
                        else if (pAttr is OpenApiRequestBodyRefAttribute oaRBra)
                        {
                            // if reference id is not set, default to parameter type name
                            if (string.IsNullOrWhiteSpace(oaRBra.ReferenceId))
                            {
                                if (paramInfo.ParameterType.Name is "Object" or null)
                                {
                                    throw new InvalidOperationException("OpenApiRequestBodyRefAttribute must have a ReferenceId specified when the parameter type is 'object'.");
                                }
                                oaRBra.ReferenceId = paramInfo.ParameterType.Name;
                            }
                            else if (paramInfo.ParameterType.Name != "Object" && oaRBra.ReferenceId != paramInfo.ParameterType.Name)
                            {
                                throw new InvalidOperationException(
                                     $"ReferenceId '{oaRBra.ReferenceId}' is different from parameter type name '{paramInfo.ParameterType.Name}'.");
                            }

                            // Retrieve the referenced request body
                            var componentRequestBody = GetRequestBody(oaRBra.ReferenceId);
                            // Determine if we inline the referenced request body or use a $ref
                            openApiAttr.RequestBody = oaRBra.Inline ? componentRequestBody.Clone() : new OpenApiRequestBodyReference(oaRBra.ReferenceId);

                            // Apply any description override
                            openApiAttr.RequestBody.Description = (oaRBra.Description is not null) ? oaRBra.Description : help.GetParameterDescription(paramInfo.Name);

                            // Add to script code parameter injection info
                            routeOptions.ScriptCode.Parameters.Add(new ParameterForInjectionInfo(paramInfo.Name, componentRequestBody));
                        }
                        else if (pAttr is OpenApiRequestBodyAttribute oaRBa)
                        {
                            var requestBody = new OpenApiRequestBody();
                            // Infer schema for the parameter type
                            var requestBodyPreferred = ComponentRequestBodiesExists(paramInfo.ParameterType.Name);

                            // Infer primitive schema if not using component
                            var schema = InferPrimitiveSchema(type: paramInfo.ParameterType, requestBodyPreferred: requestBodyPreferred, oaRBa.Inline);
                            if (CreateRequestBodyFromAttribute(attribute: oaRBa, requestBody: requestBody, schema: schema))
                            {
                                openApiAttr.RequestBody = requestBody;

                                // Apply any description override
                                openApiAttr.RequestBody.Description ??= help.GetParameterDescription(paramInfo.Name);

                                // Add to script code parameter injection info
                                routeOptions.ScriptCode.Parameters.Add(new ParameterForInjectionInfo(paramInfo.Name, requestBody));
                            }
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
                }
                // Store the OpenAPI metadata for this verb
                routeOptions.OpenAPI.Add(parsedVerb, openApiAttr);
                // Default pattern if none provided: "/<FunctionName>"
                if (string.IsNullOrWhiteSpace(routeOptions.Pattern))
                {
                    routeOptions.Pattern = "/" + func.Name;
                }
                if (!string.IsNullOrWhiteSpace(openApiAttr.CorsPolicyName))
                {
                    routeOptions.CorsPolicyName = openApiAttr.CorsPolicyName;
                }
                // Script source
                routeOptions.ScriptCode.ScriptBlock = sb;
                // Register the route
                _ = Host.AddMapRoute(routeOptions);
            }
            catch (Exception ex)
            {
                Host.Logger.Error("Error loading OpenAPI annotated function '{funcName}': {message}", func.Name, ex.Message);
            }
        }
    }
}
