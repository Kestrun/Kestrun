using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Kestrun.Runtime;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

namespace Kestrun.Mcp;

/// <summary>
/// Default route inspector implementation backed by <see cref="KestrunHost"/>.
/// </summary>
public sealed class KestrunRouteInspector : IKestrunRouteInspector
{
    internal static readonly IEqualityComparer<MapRouteOptions> RouteReferenceComparer = new MapRouteOptionsReferenceComparer();

    /// <inheritdoc />
    public IReadOnlyList<KestrunRouteSummary> ListRoutes(KestrunHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return [.. host.RegisteredRoutes.Values
            .Distinct(RouteReferenceComparer)
            .Select(CreateSummary)
            .OrderBy(static route => route.Pattern, StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc />
    public KestrunRouteDetail GetRoute(KestrunHost host, string? pattern = null, string? operationId = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        var matches = FindMatchingRoutes(host, pattern, operationId);
        if (matches.Count == 0)
        {
            return new KestrunRouteDetail
            {
                Route = EmptyRoute(pattern, operationId),
                RequestSchemas = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase),
                Responses = new Dictionary<string, KestrunRouteResponseSchema>(StringComparer.OrdinalIgnoreCase),
                Error = new KestrunMcpError(
                    "route_not_found",
                    "No route matched the requested pattern/operation id.",
                    new Dictionary<string, object?> { ["pattern"] = pattern, ["operationId"] = operationId })
            };
        }

        if (matches.Count > 1)
        {
            return new KestrunRouteDetail
            {
                Route = CreateSummary(matches[0]),
                RequestSchemas = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase),
                Responses = new Dictionary<string, KestrunRouteResponseSchema>(StringComparer.OrdinalIgnoreCase),
                Error = new KestrunMcpError(
                    "ambiguous_route",
                    "More than one route matched the request. Refine the selection with a unique pattern or operation id.",
                    new Dictionary<string, object?>
                    {
                        ["pattern"] = pattern,
                        ["operationId"] = operationId,
                        ["matches"] = matches.Select(CreateSummary).ToArray()
                    })
            };
        }

        var route = matches[0];
        return new KestrunRouteDetail
        {
            Route = CreateSummary(route),
            RequestSchemas = BuildRequestSchemas(route),
            Responses = BuildResponseSchemas(route),
            Error = null
        };
    }

    /// <summary>
    /// Finds matching routes by pattern and/or operation id.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="pattern">Optional route pattern.</param>
    /// <param name="operationId">Optional operation id.</param>
    /// <returns>The matching route options.</returns>
    private static List<MapRouteOptions> FindMatchingRoutes(KestrunHost host, string? pattern, string? operationId)
    {
        var routes = host.RegisteredRoutes.Values.Distinct(RouteReferenceComparer);

        if (!string.IsNullOrWhiteSpace(pattern))
        {
            routes = routes.Where(route => string.Equals(route.Pattern, pattern, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(operationId))
        {
            routes = routes.Where(route => route.OpenAPI.Values.Any(meta => string.Equals(meta.OperationId, operationId, StringComparison.OrdinalIgnoreCase)));
        }

        return [.. routes];
    }

    /// <summary>
    /// Builds a summarized route descriptor.
    /// </summary>
    /// <param name="route">The route options.</param>
    /// <returns>The route summary.</returns>
    internal static KestrunRouteSummary CreateSummary(MapRouteOptions route)
    {
        var metadata = route.OpenAPI.Values.FirstOrDefault();
        var responses = route.DefaultResponseContentType?
            .SelectMany(static entry => entry.Value.Select(value => value.ContentType))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        return new KestrunRouteSummary
        {
            Pattern = route.Pattern ?? "/",
            Verbs = [.. route.HttpVerbs.Select(static verb => verb.ToMethodString())],
            Tags = [.. metadata?.Tags ?? []],
            Summary = metadata?.Summary,
            Description = metadata?.Description,
            RequestContentTypes = [.. route.AllowedRequestContentTypes.Distinct(StringComparer.OrdinalIgnoreCase)],
            ResponseContentTypes = responses,
            HandlerName = route.HandlerName,
            HandlerLanguage = route.ScriptCode.Language.ToString(),
            OperationId = metadata?.OperationId
        };
    }

    /// <summary>
    /// Creates an empty route summary used in lookup failures.
    /// </summary>
    /// <param name="pattern">Requested pattern.</param>
    /// <param name="operationId">Requested operation id.</param>
    /// <returns>An empty route summary.</returns>
    private static KestrunRouteSummary EmptyRoute(string? pattern, string? operationId)
    {
        return new KestrunRouteSummary
        {
            Pattern = pattern ?? "/",
            Verbs = [],
            Tags = [],
            Summary = null,
            Description = null,
            RequestContentTypes = [],
            ResponseContentTypes = [],
            HandlerName = null,
            HandlerLanguage = null,
            OperationId = operationId
        };
    }

    /// <summary>
    /// Builds request schema payloads from OpenAPI metadata.
    /// </summary>
    /// <param name="route">The route options.</param>
    /// <returns>Request schemas keyed by content type.</returns>
    private static IReadOnlyDictionary<string, JsonNode?> BuildRequestSchemas(MapRouteOptions route)
    {
        var metadata = route.OpenAPI.Values.FirstOrDefault();
        var requestBody = metadata?.RequestBody;
        // If there is no request body, return an empty dictionary
        if (requestBody?.Content is null || requestBody.Content.Count == 0)
        {
            return new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
        }
        // Convert each request body content type to a JSON node
        return requestBody.Content.ToDictionary(
            static entry => entry.Key,
            static entry => ToJsonNode(entry.Value.Schema),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds response schema payloads from OpenAPI metadata.
    /// </summary>
    /// <param name="route">The route options.</param>
    /// <returns>Responses keyed by status code.</returns>
    private static IReadOnlyDictionary<string, KestrunRouteResponseSchema> BuildResponseSchemas(MapRouteOptions route)
    {
        var metadata = route.OpenAPI.Values.FirstOrDefault();
        var responses = metadata?.Responses;
        // If there are no responses, return an empty dictionary
        if (responses is null || responses.Count == 0)
        {
            return new Dictionary<string, KestrunRouteResponseSchema>(StringComparer.OrdinalIgnoreCase);
        }

        // Convert each response status code to a KestrunRouteResponseSchema
#pragma warning disable IDE0028 // Simplify collection initialization
        return responses.ToDictionary(
            static entry => entry.Key,
            static entry => new KestrunRouteResponseSchema
            {
                Description = entry.Value.Description,
                Content = entry.Value.Content?.ToDictionary(
                    static item => item.Key,
                    static item => ToJsonNode(item.Value.Schema),
                    StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase)
            },
            StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028 // Simplify collection initialization
    }

    /// <summary>
    /// Converts an OpenAPI element into JSON.
    /// </summary>
    /// <param name="openApiElement">The OpenAPI element to serialize.</param>
    /// <returns>A JSON node representation when available.</returns>
    private static JsonNode? ToJsonNode(IOpenApiSerializable? openApiElement)
    {
        if (openApiElement is null)
        {
            return null;
        }

        using var writer = new StringWriter();
        var jsonWriter = new OpenApiJsonWriter(writer);
        openApiElement.SerializeAsV31(jsonWriter);
        return JsonNode.Parse(writer.ToString());
    }

    /// <summary>
    /// Reference-equality comparer for route options.
    /// </summary>
    private sealed class MapRouteOptionsReferenceComparer : IEqualityComparer<MapRouteOptions>
    {
        /// <inheritdoc />
        public bool Equals(MapRouteOptions? x, MapRouteOptions? y) => ReferenceEquals(x, y);

        /// <inheritdoc />
        public int GetHashCode(MapRouteOptions obj) => RuntimeHelpers.GetHashCode(obj);
    }
}

/// <summary>
/// Default OpenAPI provider implementation backed by <see cref="KestrunHost"/>.
/// </summary>
public sealed class KestrunOpenApiProvider : IKestrunOpenApiProvider
{
    /// <inheritdoc />
    public KestrunOpenApiDocumentResult GetOpenApi(KestrunHost host, string? documentId = null, string? version = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        var docId = string.IsNullOrWhiteSpace(documentId)
            ? host.DefaultOpenApiDocumentDescriptor?.DocumentId ?? OpenApiDocDescriptor.DefaultDocumentationId
            : documentId;

        var descriptor = host.GetOrCreateOpenApiDocument(docId);

        if (!descriptor.HasBeenGenerated)
        {
            descriptor.GenerateDoc();
        }

        OpenApiSpecVersion specVersion;
        try
        {
            specVersion = string.IsNullOrWhiteSpace(version)
                ? OpenApiSpecVersion.OpenApi3_1
                : version.ParseOpenApiSpecVersion();
        }
        catch (ArgumentException ex)
        {
            return new KestrunOpenApiDocumentResult
            {
                DocumentId = docId,
                Version = version ?? OpenApiSpecVersion.OpenApi3_1.ToVersionString(),
                Error = new KestrunMcpError(
                    "unsupported_openapi_version",
                    ex.Message,
                    new Dictionary<string, object?> { ["version"] = version })
            };
        }

        return new KestrunOpenApiDocumentResult
        {
            DocumentId = docId,
            Version = specVersion.ToVersionString(),
            Document = JsonNode.Parse(descriptor.ToJson(specVersion))
        };
    }
}

/// <summary>
/// Default runtime inspector implementation backed by <see cref="KestrunHost"/>.
/// </summary>
public sealed class KestrunRuntimeInspector : IKestrunRuntimeInspector
{
    /// <inheritdoc />
    public KestrunRuntimeInspectionResult Inspect(KestrunHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return new KestrunRuntimeInspectionResult
        {
            ApplicationName = host.ApplicationName,
            Status = ResolveStatus(host),
            Environment = EnvironmentHelper.Name,
            StartTimeUtc = host.Runtime.StartTime,
            StopTimeUtc = host.Runtime.StopTime,
            Uptime = host.Runtime.Uptime,
            Listeners = [.. host.Options.Listeners.Select(CreateListener)],
            RouteCount = host.RegisteredRoutes.Count,
            Configuration = BuildSafeConfiguration(host)
        };
    }

    /// <summary>
    /// Resolves the runtime status label.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <returns>A runtime status label.</returns>
    private static string ResolveStatus(KestrunHost host)
    {
        // Determine the runtime status based on the host's state
        if (host.IsRunning)
        {
            return "running";
        }

        // Determine the runtime status based on the host's state
        if (host.IsConfigured)
        {
            return "configured";
        }

        // If the host is neither running nor configured, it is considered defined
        return "defined";
    }

    /// <summary>
    /// Builds safe listener metadata.
    /// </summary>
    /// <param name="listener">The listener options.</param>
    /// <returns>The runtime listener record.</returns>
    private static KestrunRuntimeListener CreateListener(ListenerOptions listener)
    {
        return new KestrunRuntimeListener
        {
            Url = listener.ToString(),
            Protocols = listener.Protocols.ToString(),
            UseHttps = listener.UseHttps
        };
    }

    /// <summary>
    /// Builds a safe runtime configuration snapshot.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <returns>A safe configuration snapshot.</returns>
    private static IReadOnlyDictionary<string, object?> BuildSafeConfiguration(KestrunHost host)
    {
        return new Dictionary<string, object?>
        {
            ["maxRunspaces"] = host.Options.MaxRunspaces,
            ["minRunspaces"] = host.Options.MinRunspaces,
            ["maxSchedulerRunspaces"] = host.Options.MaxSchedulerRunspaces,
            ["currentUrls"] = host.CurrentUrls,
            ["defaultResponseContentTypes"] = host.Options.DefaultResponseMediaType,
            ["defaultApiResponseContentTypes"] = host.Options.DefaultApiResponseMediaType,
            ["namedPipes"] = host.Options.NamedPipeNames,
            ["unixSockets"] = host.Options.ListenUnixSockets
        };
    }
}

/// <summary>
/// Default request validation implementation backed by route metadata.
/// </summary>
public sealed class KestrunRequestValidator(IKestrunRouteInspector routeInspector) : IKestrunRequestValidator
{
    private readonly IKestrunRouteInspector _routeInspector = routeInspector ?? throw new ArgumentNullException(nameof(routeInspector));

    /// <inheritdoc />
    public KestrunRequestValidationResult Validate(KestrunHost host, KestrunRequestInput input)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(input);

        var method = NormalizeMethod(input.Method);
        var requestPath = NormalizePath(input.Path);
        var matches = FindPathMatches(host, requestPath);
        if (matches.Count == 0)
        {
            return Failure(404, "No registered route matches the requested path.", "route_not_found");
        }

        var route = matches.FirstOrDefault(candidate => candidate.HttpVerbs.Any(verb => string.Equals(verb.ToMethodString(), method, StringComparison.OrdinalIgnoreCase)));
        if (route is null)
        {
            var allowedMethods = matches.SelectMany(static candidate => candidate.HttpVerbs).Select(static verb => verb.ToMethodString()).Distinct().ToArray();
            return Failure(
                404,
                $"No route matched method '{method}' for path '{requestPath}'. Registered methods: {string.Join(", ", allowedMethods)}.",
                "method_not_matched");
        }

        var contentTypeValidation = ValidateContentType(route, input);
        if (contentTypeValidation is not null)
        {
            return contentTypeValidation;
        }

        var acceptValidation = ValidateAccept(route, input);
        // If the route has OpenAPI annotations, validate the Accept header.
        if (acceptValidation is not null)
        {
            return acceptValidation;
        }

        // If we reach this point, the request is valid.
        return new KestrunRequestValidationResult
        {
            IsValid = true,
            StatusCode = 200,
            Message = "The request matches a registered route and satisfies known content-type/accept constraints.",
            Route = _routeInspector.GetRoute(host, route.Pattern).Route
        };
    }

    /// <summary>
    /// Validates request content type constraints.
    /// </summary>
    /// <param name="route">The selected route.</param>
    /// <param name="input">The request input.</param>
    /// <returns>A failure result when validation fails; otherwise null.</returns>
    private KestrunRequestValidationResult? ValidateContentType(MapRouteOptions route, KestrunRequestInput input)
    {
        if (route.AllowedRequestContentTypes.Count == 0)
        {
            return null;
        }

        var headers = NormalizeHeaders(input.Headers);
        var hasBody = HasBody(input.Body);
        _ = headers.TryGetValue(HeaderNames.ContentType, out var contentType);

        if (string.IsNullOrWhiteSpace(contentType))
        {
            return !hasBody
                ? null
                : Failure(415, $"Content-Type is required. Supported types: {string.Join(", ", route.AllowedRequestContentTypes)}.", "missing_content_type");
        }

        if (!MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
        {
            return Failure(400, $"Content-Type header '{contentType}' is malformed.", "invalid_content_type");
        }

        var raw = mediaType.MediaType.ToString();
        var canonical = MediaTypeHelper.Canonicalize(raw);
        var allowed = route.AllowedRequestContentTypes.Any(candidate =>
            string.Equals(candidate, raw, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(MediaTypeHelper.Canonicalize(candidate), canonical, StringComparison.OrdinalIgnoreCase));

        return allowed
            ? null
            : Failure(415, $"Content-Type '{raw}' is not allowed. Supported types: {string.Join(", ", route.AllowedRequestContentTypes)}.", "unsupported_media_type");
    }

    /// <summary>
    /// Validates Accept-header constraints for OpenAPI-aware routes.
    /// </summary>
    /// <param name="route">The selected route.</param>
    /// <param name="input">The request input.</param>
    /// <returns>A failure result when validation fails; otherwise null.</returns>
    private static KestrunRequestValidationResult? ValidateAccept(MapRouteOptions route, KestrunRequestInput input)
    {
        if (!route.IsOpenApiAnnotatedFunctionRoute)
        {
            return null;
        }

        var headers = NormalizeHeaders(input.Headers);
        if (!headers.TryGetValue(HeaderNames.Accept, out var acceptHeader) || string.IsNullOrWhiteSpace(acceptHeader))
        {
            return null;
        }

        var supported = ResolveResponseContentTypes(route);
        if (supported.Count == 0)
        {
            return null;
        }

        var selected = SelectResponseMediaType(acceptHeader, supported, supported[0].ContentType);
        return selected is not null
            ? null
            : Failure(406, $"Accept header '{acceptHeader}' is not compatible with the route response types: {string.Join(", ", supported.Select(static value => value.ContentType))}.", "not_acceptable");
    }

    /// <summary>
    /// Finds registered routes whose template matches the provided path.
    /// </summary>
    /// <param name="host">The Kestrun host.</param>
    /// <param name="requestPath">The request path.</param>
    /// <returns>The matching routes.</returns>
    private static List<MapRouteOptions> FindPathMatches(KestrunHost host, string requestPath)
    {
        return [.. host.RegisteredRoutes.Values
            .Distinct(KestrunRouteInspector.RouteReferenceComparer)
            .Where(route => RoutePatternMatches(route.Pattern, requestPath))];
    }

    /// <summary>
    /// Determines whether a route pattern matches the supplied request path.
    /// </summary>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="requestPath">The request path.</param>
    /// <returns>True when the path matches the route template.</returns>
    private static bool RoutePatternMatches(string? pattern, string requestPath)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var template = TemplateParser.Parse(pattern);
        var matcher = new TemplateMatcher(template, []);
        return matcher.TryMatch(requestPath, []);
    }

    /// <summary>
    /// Resolves likely response content types for validation.
    /// </summary>
    /// <param name="route">The route options.</param>
    /// <returns>The likely successful response content types.</returns>
    internal static IReadOnlyList<ContentTypeWithSchema> ResolveResponseContentTypes(MapRouteOptions route)
    {
        return TryGetResponseContentTypes(route.DefaultResponseContentType, StatusCodes.Status200OK, out var values) && values is not null
            ? values as IReadOnlyList<ContentTypeWithSchema> ?? [.. values]
            : [];
    }

    /// <summary>
    /// Normalizes request method values.
    /// </summary>
    /// <param name="method">The incoming method.</param>
    /// <returns>A normalized method.</returns>
    private static string NormalizeMethod(string? method)
        => string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant();

    /// <summary>
    /// Normalizes request path values.
    /// </summary>
    /// <param name="path">The incoming path.</param>
    /// <returns>A normalized path.</returns>
    private static string NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? "/" : path.StartsWith('/') ? path : "/" + path;

    /// <summary>
    /// Normalizes headers into a case-insensitive dictionary.
    /// </summary>
    /// <param name="headers">The incoming headers.</param>
    /// <returns>A normalized header dictionary.</returns>
    private static Dictionary<string, string> NormalizeHeaders(IDictionary<string, string>? headers)
#pragma warning disable IDE0028 // Simplify collection initialization
        => headers is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028 // Simplify collection initialization

    /// <summary>
    /// Determines whether a request body is present.
    /// </summary>
    /// <param name="body">The request body.</param>
    /// <returns>True when a body is present.</returns>
    private static bool HasBody(object? body)
    {
        return body switch
        {
            null => false,
            string text => !string.IsNullOrEmpty(text),
            JsonNode => true,
            _ => true
        };
    }

    /// <summary>
    /// Creates a standardized failure payload.
    /// </summary>
    /// <param name="statusCode">The likely status code.</param>
    /// <param name="message">The validation message.</param>
    /// <param name="code">The stable error code.</param>
    /// <returns>A failure result.</returns>
    private static KestrunRequestValidationResult Failure(int statusCode, string message, string code)
    {
        return new KestrunRequestValidationResult
        {
            IsValid = false,
            StatusCode = statusCode,
            Message = message,
            Error = new KestrunMcpError(code, message)
        };
    }

    /// <summary>
    /// Resolves response content types for a status code using exact, range, then default lookup.
    /// </summary>
    /// <param name="contentTypes">Configured content type mappings.</param>
    /// <param name="statusCode">The status code to resolve.</param>
    /// <param name="values">Resolved content types when available.</param>
    /// <returns>True when a mapping exists.</returns>
    internal static bool TryGetResponseContentTypes(
        IDictionary<string, ICollection<ContentTypeWithSchema>>? contentTypes,
        int statusCode,
        out ICollection<ContentTypeWithSchema>? values)
    {
        values = null;
        if (contentTypes is null || contentTypes.Count == 0)
        {
            return false;
        }

        var statusKey = statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (TryGetValueIgnoreCase(contentTypes, statusKey, out values))
        {
            return true;
        }

        if (statusCode is >= 100 and <= 599)
        {
            var rangeKey = $"{statusCode / 100}XX";
            if (TryGetValueIgnoreCase(contentTypes, rangeKey, out values))
            {
                return true;
            }
        }

        return TryGetValueIgnoreCase(contentTypes, "default", out values);
    }

    /// <summary>
    /// Performs a case-insensitive dictionary lookup.
    /// </summary>
    /// <param name="dictionary">The source dictionary.</param>
    /// <param name="key">The requested key.</param>
    /// <param name="value">The resolved value when present.</param>
    /// <returns>True when a value exists.</returns>
    internal static bool TryGetValueIgnoreCase<TValue>(IDictionary<string, TValue> dictionary, string key, out TValue? value)
    {
        foreach (var entry in dictionary)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Selects the most appropriate response media type for a supplied Accept header.
    /// </summary>
    /// <param name="acceptHeader">The incoming Accept header value.</param>
    /// <param name="supported">Supported response media types.</param>
    /// <param name="defaultType">Fallback type when <c>*/*</c> is configured.</param>
    /// <returns>The selected content type or null when no match exists.</returns>
    internal static ContentTypeWithSchema? SelectResponseMediaType(string? acceptHeader, IReadOnlyList<ContentTypeWithSchema> supported, string defaultType)
    {
        if (supported.Count == 0)
        {
            return new ContentTypeWithSchema(defaultType, null);
        }

        if (string.IsNullOrWhiteSpace(acceptHeader))
        {
            return supported[0];
        }

        if (!MediaTypeHeaderValue.TryParseList([acceptHeader], out var accepts) || accepts.Count == 0)
        {
            return supported[0];
        }

        var supportsAnyMediaType = supported.Any(static value => string.Equals(MediaTypeHelper.Normalize(value.ContentType), "*/*", StringComparison.OrdinalIgnoreCase));
        var normalizedSupported = supported.Select(static value => MediaTypeHelper.Normalize(value.ContentType)).ToArray();
        var canonicalSupported = supported.Select(static value => MediaTypeHelper.Canonicalize(value.ContentType)).ToArray();

        foreach (var candidate in accepts.OrderByDescending(static value => value.Quality ?? 1.0))
        {
            var accept = candidate.MediaType.Value;
            if (accept is null)
            {
                continue;
            }

            var normalizedAccept = MediaTypeHelper.Normalize(accept);
            if (supportsAnyMediaType)
            {
                return SelectWhenAnySupported(normalizedAccept, defaultType);
            }

            var selected = SelectConfiguredMediaType(normalizedAccept, supported, normalizedSupported, canonicalSupported);
            if (selected is not null)
            {
                return selected;
            }
        }

        return null;
    }

    /// <summary>
    /// Selects a media type when the route supports any response media type.
    /// </summary>
    /// <param name="normalizedAccept">The normalized Accept header value.</param>
    /// <param name="defaultType">The fallback response type.</param>
    /// <returns>The selected content type entry.</returns>
    private static ContentTypeWithSchema SelectWhenAnySupported(string normalizedAccept, string defaultType)
    {
        // If the Accept header is a wildcard, return the default type.
        if (string.Equals(normalizedAccept, "*/*", StringComparison.OrdinalIgnoreCase) ||
            normalizedAccept.EndsWith("/*", StringComparison.OrdinalIgnoreCase))
        {
            return new ContentTypeWithSchema(defaultType, null);
        }

        // If the Accept header is not a wildcard, resolve a concrete media type.
        return new ContentTypeWithSchema(ResolveWriterMediaType(normalizedAccept, defaultType), null);
    }

    /// <summary>
    /// Selects a configured media type for an Accept header.
    /// </summary>
    /// <param name="normalizedAccept">The normalized Accept header value.</param>
    /// <param name="supported">Supported media types.</param>
    /// <param name="normalizedSupported">Normalized supported media types.</param>
    /// <param name="canonicalSupported">Canonical supported media types.</param>
    /// <returns>The selected media type entry when available.</returns>
    private static ContentTypeWithSchema? SelectConfiguredMediaType(
        string normalizedAccept,
        IReadOnlyList<ContentTypeWithSchema> supported,
        IReadOnlyList<string> normalizedSupported,
        IReadOnlyList<string> canonicalSupported)
    {
        if (string.Equals(normalizedAccept, "*/*", StringComparison.OrdinalIgnoreCase))
        {
            return supported[0];
        }

        if (normalizedAccept.EndsWith("/*", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = normalizedAccept[..^1];
            for (var i = 0; i < supported.Count; i++)
            {
                if (normalizedSupported[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return supported[i];
                }
            }

            return null;
        }

        var canonicalAccept = MediaTypeHelper.Canonicalize(normalizedAccept);
        for (var i = 0; i < supported.Count; i++)
        {
            if (string.Equals(normalizedSupported[i], normalizedAccept, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(canonicalSupported[i], canonicalAccept, StringComparison.OrdinalIgnoreCase))
            {
                return supported[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a concrete writer media type for wildcard routes.
    /// </summary>
    /// <param name="normalizedAccept">The normalized Accept header value.</param>
    /// <param name="defaultType">The fallback type.</param>
    /// <returns>A concrete writer media type.</returns>
    private static string ResolveWriterMediaType(string normalizedAccept, string defaultType)
    {
        var canonical = MediaTypeHelper.Canonicalize(normalizedAccept);
        // If the Accept header is a known canonical type, return it.
        if (string.Equals(canonical, "application/json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonical, "application/xml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(canonical, "application/yaml", StringComparison.OrdinalIgnoreCase))
        {
            return canonical;
        }

        // If the Accept header is a specific type, return it.
        if (string.Equals(normalizedAccept, "text/csv", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedAccept, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedAccept;
        }

        // If the Accept header is a text type, return text/plain.
        return normalizedAccept.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ? "text/plain" : defaultType;
    }
}

/// <summary>
/// Default route invoker implementation that uses the live HTTP pipeline.
/// </summary>
public sealed class KestrunRequestInvoker(
    IKestrunRequestValidator validator,
    KestrunRequestInvokerOptions options) : IKestrunRequestInvoker
{
    private readonly IKestrunRequestValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    private readonly KestrunRequestInvokerOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async Task<KestrunRouteInvokeResult> InvokeAsync(KestrunHost host, KestrunRequestInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(input);

        var path = NormalizePath(input.Path);
        if (!_options.EnableInvocation)
        {
            return Failure(403, "Route invocation is disabled for this MCP server.", "invoke_disabled");
        }

        if (!IsPathAllowed(path))
        {
            return Failure(403, $"Route '{path}' is not allowlisted for invocation.", "invoke_not_allowlisted");
        }

        var validation = _validator.Validate(host, input);
        if (!validation.IsValid)
        {
            return new KestrunRouteInvokeResult
            {
                StatusCode = validation.StatusCode,
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Error = validation.Error
            };
        }

        var baseUri = ResolveBaseUri(host);
        if (baseUri is null)
        {
            return Failure(503, "The Kestrun host is not running on a known listener URL.", "runtime_not_available");
        }

        try
        {
            using var httpClient = new HttpClient { BaseAddress = baseUri };
            using var request = BuildRequestMessage(input);
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var contentType = response.Content.Headers.ContentType?.ToString();
            var body = response.Content is null ? null : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new KestrunRouteInvokeResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = contentType,
                Headers = RedactHeaders(response),
                Body = body
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Failure(500, $"Route invocation failed: {ex.Message}", "invoke_failed");
        }
    }

    /// <summary>
    /// Builds an HTTP request message from the supplied invocation input.
    /// </summary>
    /// <param name="input">The invocation input.</param>
    /// <returns>The request message.</returns>
    private static HttpRequestMessage BuildRequestMessage(KestrunRequestInput input)
    {
        var uri = BuildRelativeUri(input.Path, input.Query);
        var request = new HttpRequestMessage(new HttpMethod(string.IsNullOrWhiteSpace(input.Method) ? "GET" : input.Method), uri);
        var headers = input.Headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var contentType = headers.TryGetValue(HeaderNames.ContentType, out var value) ? value : null;

        foreach (var header in headers)
        {
            if (string.Equals(header.Key, HeaderNames.ContentType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                request.Content ??= new StringContent(string.Empty, Encoding.UTF8);
                _ = request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (input.Body is not null)
        {
            request.Content = CreateContent(input.Body, contentType);
        }

        return request;
    }

    /// <summary>
    /// Creates HTTP content for the invocation request.
    /// </summary>
    /// <param name="body">The request body.</param>
    /// <param name="contentType">The request content type.</param>
    /// <returns>The HTTP content.</returns>
    private static HttpContent CreateContent(object body, string? contentType)
    {
        if (body is string text)
        {
            var mediaType = string.IsNullOrWhiteSpace(contentType) ? "text/plain" : contentType;
            return new StringContent(text, Encoding.UTF8, mediaType);
        }

        var json = JsonSerializer.Serialize(body);
        return new StringContent(json, Encoding.UTF8, string.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType);
    }

    /// <summary>
    /// Builds a relative request URI from path and query values.
    /// </summary>
    /// <param name="path">The route path.</param>
    /// <param name="query">Optional query values.</param>
    /// <returns>The request URI.</returns>
    private static string BuildRelativeUri(string? path, IDictionary<string, string>? query)
    {
        var builder = new StringBuilder(NormalizePath(path));
        if (query is null || query.Count == 0)
        {
            return builder.ToString();
        }

        var first = true;
        foreach (var pair in query)
        {
            _ = builder.Append(first ? '?' : '&').
            Append(WebUtility.UrlEncode(pair.Key)).
            Append('=').
            Append(WebUtility.UrlEncode(pair.Value));
            first = false;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Resolves the first known loopback/base URL for the host.
    /// </summary>
    /// <param name="host">The running host.</param>
    /// <returns>The resolved base URI when available.</returns>
    private static Uri? ResolveBaseUri(KestrunHost host)
    {
        foreach (var url in host.CurrentUrls)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return NormalizeLoopbackUri(uri);
            }
        }

        return null;
    }

    /// <summary>
    /// Rewrites wildcard listener addresses to a loopback address suitable for local invocation.
    /// </summary>
    /// <param name="uri">The candidate listener URI.</param>
    /// <returns>The rewritten URI when needed; otherwise the original URI.</returns>
    private static Uri NormalizeLoopbackUri(Uri uri)
    {
        if (!string.Equals(uri.Host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Host, "::", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Host, "[::]", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Host, "*", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        var builder = new UriBuilder(uri)
        {
            Host = uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? "localhost"
                : IPAddress.Loopback.ToString()
        };

        return builder.Uri;
    }

    /// <summary>
    /// Redacts configured sensitive headers from response output.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <returns>A redacted header dictionary.</returns>
    private IReadOnlyDictionary<string, string> RedactHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
        {
            headers[header.Key] = RedactIfSensitive(header.Key, string.Join(", ", header.Value));
        }

        if (response.Content is not null)
        {
            foreach (var header in response.Content.Headers)
            {
                headers[header.Key] = RedactIfSensitive(header.Key, string.Join(", ", header.Value));
            }
        }

        return headers;
    }

    /// <summary>
    /// Determines whether a path is allowlisted for invocation.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns>True when the path is allowlisted.</returns>
    private bool IsPathAllowed(string path)
    {
        // If no allowed path patterns are configured, deny all paths.
        if (_options.AllowedPathPatterns.Count == 0)
        {
            return false;
        }

        // Check if the path matches any of the allowed patterns.
        return _options.AllowedPathPatterns.Any(pattern =>
            string.Equals(pattern, "*", StringComparison.Ordinal) ||
            GlobMatches(pattern, path));
    }

    /// <summary>
    /// Redacts sensitive header values.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The original header value.</param>
    /// <returns>The redacted or original value.</returns>
    private string RedactIfSensitive(string name, string value)
        => _options.RedactedHeaders.Contains(name) ? "[REDACTED]" : value;

    /// <summary>
    /// Matches a glob-style path pattern.
    /// </summary>
    /// <param name="pattern">The glob pattern.</param>
    /// <param name="value">The candidate value.</param>
    /// <returns>True when the value matches the pattern.</returns>
    private static bool GlobMatches(string pattern, string value)
    {
        var regex = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(value, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Creates a standardized invocation failure payload.
    /// </summary>
    /// <param name="statusCode">The failure status code.</param>
    /// <param name="message">The failure message.</param>
    /// <param name="code">The stable error code.</param>
    /// <returns>A failure result.</returns>
    private static KestrunRouteInvokeResult Failure(int statusCode, string message, string code)
    {
        return new KestrunRouteInvokeResult
        {
            StatusCode = statusCode,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Error = new KestrunMcpError(code, message)
        };
    }

    /// <summary>
    /// Normalizes request paths.
    /// </summary>
    /// <param name="path">The incoming path.</param>
    /// <returns>A normalized path.</returns>
    private static string NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? "/" : path.StartsWith('/') ? path : "/" + path;
}
