using System.Management.Automation;
using Kestrun.Forms;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Kestrun.Scripting;
using Kestrun.Utilities;
using Microsoft.OpenApi;

namespace Kestrun.Hosting;

public static partial class KestrunHostMapExtensions
{
    /// <summary>
    /// Adds a POST route that parses form payloads using <see cref="KrFormParser"/>, injects the parsed payload into the
    /// runspace as <c>$FormPayload</c>, and then
    /// executes the provided PowerShell <paramref name="userScriptBlock"/>.
    ///
    /// By default, only <c>multipart/form-data</c> is accepted; additional request content types (such as
    /// <c>application/x-www-form-urlencoded</c> and <c>multipart/mixed</c>) are opt-in via
    /// <see cref="KrFormOptions.AllowedRequestContentTypes"/>.
    ///
    /// This method also fills <see cref="MapRouteOptions.OpenAPI"/> (unless disabled) so the route appears in generated
    /// OpenAPI documents.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="pattern">The route pattern (e.g. <c>/upload</c>).</param>
    /// <param name="userScriptBlock">The PowerShell scriptblock to execute after parsing.</param>
    /// <param name="formOptions">Form parsing options (null uses defaults).</param>
    /// <param name="authorizationSchemes">Authorization schemes (optional).</param>
    /// <param name="authorizationPolicies">Authorization policies (optional).</param>
    /// <param name="corsPolicy">CORS policy name (optional).</param>
    /// <param name="allowAnonymous">Whether to allow anonymous access.</param>
    /// <returns>The host for chaining.</returns>
    public static KestrunHost AddFormRoute(
        this KestrunHost host,
        string pattern,
        ScriptBlock userScriptBlock,
        KrFormOptions? formOptions,
        string[]? authorizationSchemes,
        string[]? authorizationPolicies,
        string? corsPolicy,
        bool allowAnonymous)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentNullException.ThrowIfNull(userScriptBlock);

        formOptions ??= new KrFormOptions();
        formOptions.Logger ??= host.Logger;

        var mapOptions = BuildFormRouteMapOptions(
            pattern,
            userScriptBlock,
            formOptions,
            authorizationSchemes,
            authorizationPolicies,
            corsPolicy,
            allowAnonymous);

        if (host.RouteGroupStack.Count > 0 && host.RouteGroupStack.Peek() is MapRouteOptions parent)
        {
            mapOptions = MergeMapRouteOptions(parent, mapOptions);
        }

        return host.AddMapRoute(mapOptions);
    }

    /// <summary>
    /// Creates language options for the form route script.
    /// </summary>
    /// <param name="formOptions">Form parsing options.</param>
    /// <param name="userScriptBlock">The PowerShell scriptblock to execute after parsing.</param>
    /// <returns>Language options for the script.</returns>
    private static LanguageOptions CreateLanguageOptions(KrFormOptions formOptions, ScriptBlock userScriptBlock)
    {
        return new LanguageOptions
        {
            Language = ScriptLanguage.PowerShell,
            Code = GetFormRouteWrapperScript(userScriptBlock),
            Arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["__KrOptions"] = formOptions
            }
        };
    }

    private static MapRouteOptions BuildFormRouteMapOptions(
        string pattern,
        ScriptBlock userScriptBlock,
        KrFormOptions formOptions,
        string[]? authorizationSchemes,
        string[]? authorizationPolicies,
        string? corsPolicy,
        bool allowAnonymous,
        bool disableOpenApi = true,
        string? openApiOperationId = null,
        string[]? openApiTags = null,
        string? openApiSummary = null,
        string? openApiDescription = null,
        string[]? openApiDocumentId = null
      )
    {
        var routeOptions = new MapRouteOptions
        {
            Pattern = pattern,
            HttpVerbs = [HttpVerb.Post],
            AllowAnonymous = allowAnonymous,
            CorsPolicy = corsPolicy ?? string.Empty,
            ScriptCode = CreateLanguageOptions(formOptions, userScriptBlock),
            FormOptions = formOptions
        };

        if (!allowAnonymous)
        {
            if (authorizationSchemes is { Length: > 0 })
            {
                routeOptions.RequireSchemes.AddRange(authorizationSchemes ?? []);
            }

            if (authorizationPolicies is { Length: > 0 })
            {
                routeOptions.RequirePolicies.AddRange(authorizationPolicies ?? []);
            }
        }
        else if (authorizationSchemes is { Length: > 0 } || authorizationPolicies is { Length: > 0 })
        {
            throw new ArgumentException(
                "The allowAnonymous flag cannot be used together with authorizationSchemes or authorizationPolicies.");
        }

        if (!disableOpenApi)
        {
            var meta = new OpenAPIPathMetadata(pattern, routeOptions)
            {
                PathLikeKind = OpenApiPathLikeKind.Path,
                OperationId = string.IsNullOrWhiteSpace(openApiOperationId) ? null : openApiOperationId,
                Summary = string.IsNullOrWhiteSpace(openApiSummary) ? null : openApiSummary,
                Description = string.IsNullOrWhiteSpace(openApiDescription) ? null : openApiDescription,
                DocumentId = openApiDocumentId
            };

            if (openApiTags is { Length: > 0 })
            {
                meta.Tags.AddRange(openApiTags ?? []);
            }

            meta.RequestBody = BuildOpenApiRequestBody(formOptions);

            // Let the descriptor generate a default 200 response.
            meta.Responses = null;

            routeOptions.OpenAPI[HttpVerb.Post] = meta;
        }

        return routeOptions;
    }

    private static string GetFormRouteWrapperScript(ScriptBlock scriptBlock)
    {
        // NOTE: We recreate the ScriptBlock inside the request runspace so it executes with the request's
        // session state (including $Context and Kestrun cmdlets).
        return @"
##############################
# Form Route Wrapper
##############################
$FormPayload = $null
try {
    $FormPayload = [Kestrun.Forms.KrFormParser]::Parse($Context.HttpContext, $__KrOptions, $Context.Ct)
} catch [Kestrun.Forms.KrFormException] {
    $ex = $_.Exception
    Write-KrTextResponse -InputObject $ex.Message -StatusCode $ex.StatusCode
    return
}

############################
# User Scriptblock
############################

" + scriptBlock.ToString();
    }

    internal static OpenApiRequestBody BuildOpenApiRequestBody(KrFormOptions options)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);

        var multipartProps = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        var urlEncodedProps = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        var multipartEncoding = new Dictionary<string, OpenApiEncoding>(StringComparer.Ordinal);

        foreach (var rule in options.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                continue;
            }

            if (rule.Required)
            {
                _ = required.Add(rule.Name);
            }

            var isFile = IsProbablyFileRule(rule);
            var multipartSchema = CreateRuleSchema(isFile, rule.AllowMultiple);
            multipartProps[rule.Name] = multipartSchema;

            if (isFile && rule.AllowedContentTypes.Count > 0)
            {
                multipartEncoding[rule.Name] = new OpenApiEncoding
                {
                    ContentType = string.Join(", ", rule.AllowedContentTypes)
                };
            }

            // application/x-www-form-urlencoded can't carry binary file parts; model values as string (or array of strings).
            urlEncodedProps[rule.Name] = CreateRuleSchema(isFile: false, allowMultiple: rule.AllowMultiple);
        }

        var multipartObject = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = multipartProps,
            Required = required.Count > 0 ? required : null
        };

        var urlEncodedObject = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = urlEncodedProps,
            Required = required.Count > 0 ? required : null
        };

        var content = new Dictionary<string, IOpenApiMediaType>(StringComparer.OrdinalIgnoreCase);
        foreach (var ct in options.AllowedRequestContentTypes)
        {
            if (string.IsNullOrWhiteSpace(ct))
            {
                continue;
            }

            var isUrlEncoded = ct.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
            var mediaType = new OpenApiMediaType
            {
                Schema = isUrlEncoded ? urlEncodedObject : multipartObject
            };

            if (!isUrlEncoded && multipartEncoding.Count > 0)
            {
                mediaType.Encoding = multipartEncoding;
            }

            content[ct] = mediaType;
        }

        return new OpenApiRequestBody
        {
            Required = true,
            Description = "Form payload (multipart/* and/or application/x-www-form-urlencoded), parsed into $FormPayload.",
            Content = content
        };
    }

    private static bool IsProbablyFileRule(KrFormPartRule rule)
    {
        if (rule.StoreToDisk)
        {
            return true;
        }

        if (rule.AllowedExtensions.Count > 0)
        {
            return true;
        }

        foreach (var ct in rule.AllowedContentTypes)
        {
            if (string.IsNullOrWhiteSpace(ct))
            {
                continue;
            }

            if (!ct.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                && !ct.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                && !ct.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IOpenApiSchema CreateRuleSchema(bool isFile, bool allowMultiple)
    {
        var baseSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = isFile ? "binary" : null
        };

        if (!allowMultiple)
        {
            return baseSchema;
        }
        // Returns an array
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = baseSchema
        };
    }

    private static MapRouteOptions MergeMapRouteOptions(MapRouteOptions parent, MapRouteOptions child)
    {
        var mergedPattern = MergePattern(parent.Pattern, child.Pattern);

        var merged = new MapRouteOptions
        {
            Pattern = mergedPattern,
            HttpVerbs = child.HttpVerbs is { Count: > 0 } ? child.HttpVerbs : parent.HttpVerbs,
            CorsPolicy = !string.IsNullOrWhiteSpace(child.CorsPolicy) ? child.CorsPolicy : parent.CorsPolicy,
            ThrowOnDuplicate = child.ThrowOnDuplicate || parent.ThrowOnDuplicate,
            AllowAnonymous = child.AllowAnonymous || parent.AllowAnonymous,
            ScriptCode = new LanguageOptions
            {
                Code = !string.IsNullOrWhiteSpace(child.ScriptCode.Code) ? child.ScriptCode.Code : parent.ScriptCode.Code,
                Language = child.ScriptCode.Language != default ? child.ScriptCode.Language : parent.ScriptCode.Language,
                ExtraImports = MergeUnique(parent.ScriptCode.ExtraImports, child.ScriptCode.ExtraImports),
                ExtraRefs = MergeRefs(parent.ScriptCode.ExtraRefs, child.ScriptCode.ExtraRefs),
                Arguments = MergeArguments(parent.ScriptCode.Arguments, child.ScriptCode.Arguments)
            },
            OpenAPI = child.OpenAPI is { Count: > 0 } ? child.OpenAPI : parent.OpenAPI,
        };

        var mergedSchemes = MergeUnique([.. parent.RequireSchemes], [.. child.RequireSchemes]);
        merged.RequireSchemes.AddRange(mergedSchemes ?? []);

        var mergedPolicies = MergeUnique([.. parent.RequirePolicies], [.. child.RequirePolicies]);
        merged.RequirePolicies.AddRange(mergedPolicies ?? []);

        return merged;
    }

    private static string? MergePattern(string? parentPattern, string? childPattern)
    {
        if (string.IsNullOrWhiteSpace(childPattern))
        {
            return parentPattern;
        }

        if (string.IsNullOrWhiteSpace(parentPattern))
        {
            return childPattern;
        }

        // Returns a combined pattern, ensuring no double slashes.
        return $"{parentPattern}/{childPattern}".Replace("//", "/", StringComparison.Ordinal);
    }

    private static string[]? MergeUnique(string[]? a, string[]? b)
    {
        if (a is null && b is null)
        {
            return null;
        }

        var set = new HashSet<string>(StringComparer.Ordinal);
        if (a is not null)
        {
            foreach (var s in a)
            {
                if (!string.IsNullOrWhiteSpace(s))
                {
                    _ = set.Add(s);
                }
            }
        }

        if (b is not null)
        {
            foreach (var s in b)
            {
                if (!string.IsNullOrWhiteSpace(s))
                {
                    _ = set.Add(s);
                }
            }
        }

        return set.Count == 0 ? [] : [.. set];
    }

    private static System.Reflection.Assembly[]? MergeRefs(System.Reflection.Assembly[]? a, System.Reflection.Assembly[]? b)
    {
        if (b is null || b.Length == 0)
        {
            return a;
        }

        if (a is null || a.Length == 0)
        {
            return b;
        }
        // Returns
        return [.. a, .. b];
    }

    private static Dictionary<string, object?> MergeArguments(Dictionary<string, object?>? a, Dictionary<string, object?>? b)
    {
        if (a is null || a.Count == 0)
        {
            return b is null ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, object?>(b, StringComparer.OrdinalIgnoreCase);
        }

        if (b is null || b.Count == 0)
        {
            return new Dictionary<string, object?>(a, StringComparer.OrdinalIgnoreCase);
        }

        var m = new Dictionary<string, object?>(a, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in b)
        {
            m[k] = v;
        }

        return m;
    }
}
