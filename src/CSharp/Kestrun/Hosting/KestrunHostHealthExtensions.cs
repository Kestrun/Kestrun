using Kestrun.Health;
using Kestrun.Hosting.Options;
using Kestrun.Models;
using Kestrun.Scripting;
using Kestrun.Utilities;
using Microsoft.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kestrun.Hosting;

/// <summary>
/// Adds health-check specific helpers to <see cref="KestrunHost"/>.
/// </summary>
public static class KestrunHostHealthExtensions
{
    private static readonly JsonSerializerOptions JsonOptions;

    static KestrunHostHealthExtensions()
    {
        JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    /// <summary>
    /// Registers a GET endpoint (default <c>/health</c>) that aggregates the state of all registered probes.
    /// </summary>
    /// <param name="host">The host to configure.</param>
    /// <param name="configure">Optional action to mutate the default endpoint options.</param>
    /// <returns>The <see cref="KestrunHost"/> instance for fluent chaining.</returns>
    public static KestrunHost AddHealthEndpoint(this KestrunHost host, Action<HealthEndpointOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        var merged = host.Options.Health?.Clone() ?? new HealthEndpointOptions();
        configure?.Invoke(merged);

        var mapOptions = new MapRouteOptions
        {
            Pattern = merged.Pattern,
            HttpVerbs = [HttpVerb.Get],
            Language = ScriptLanguage.Native,
            AllowAnonymous = merged.AllowAnonymous,
            DisableAntiforgery = true,
            RequireSchemes = merged.RequireSchemes,
            RequirePolicies = merged.RequirePolicies,
            CorsPolicyName = merged.CorsPolicyName ?? string.Empty,
            RateLimitPolicyName = merged.RateLimitPolicyName,
            ShortCircuit = merged.ShortCircuit,
            ShortCircuitStatusCode = merged.ShortCircuitStatusCode,
            ThrowOnDuplicate = merged.ThrowOnDuplicate,
            OpenAPI = new MapRouteOptions.OpenAPIMetadata
            {
                Summary = merged.OpenApiSummary,
                Description = merged.OpenApiDescription,
                OperationId = merged.OpenApiOperationId,
                Tags = merged.OpenApiTags,
                GroupName = merged.OpenApiGroupName
            }
        };

        // Auto-register endpoint only when enabled
        if (!merged.AutoRegisterEndpoint)
        {
            host.HostLogger.Debug("Health endpoint AutoRegisterEndpoint=false; skipping automatic mapping for pattern {Pattern}", merged.Pattern);
            return host;
        }

        // If the app pipeline is already built/configured, map immediately; otherwise defer until build
        if (host.IsConfigured)
        {
            MapHealthEndpointImmediate(host, merged, mapOptions);
            return host;
        }

        return host.Use(app => MapHealthEndpointImmediate(host, merged, mapOptions));
    }

    /// <summary>
    /// Registers a GET endpoint (default <c>/health</c>) using a pre-configured <see cref="HealthEndpointOptions"/> instance.
    /// </summary>
    /// <param name="host">The host to configure.</param>
    /// <param name="options">A fully configured options object.</param>
    /// <returns>The <see cref="KestrunHost"/> instance for fluent chaining.</returns>
    public static KestrunHost AddHealthEndpoint(this KestrunHost host, HealthEndpointOptions options)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(options);

        return host.AddHealthEndpoint(dest => CopyHealthEndpointOptions(options, dest));
    }

    // ApplyConventions removed; unified with KestrunHostMapExtensions.AddMapOptions

    private static string[] ExtractTags(HttpRequest request)
    {
        var collected = new List<string>();
        if (request.Query.TryGetValue("tag", out var singleValues))
        {
            foreach (var value in singleValues)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    collected.AddRange(value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                }
            }
        }

        if (request.Query.TryGetValue("tags", out var multiValues))
        {
            foreach (var value in multiValues)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    collected.AddRange(value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                }
            }
        }

        return collected.Count == 0
            ? []
            : [.. collected.Where(static t => !string.IsNullOrWhiteSpace(t))
                           .Select(static t => t.Trim())
                           .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static void CopyHealthEndpointOptions(HealthEndpointOptions source, HealthEndpointOptions target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        target.Pattern = source.Pattern;
        target.DefaultTags = source.DefaultTags is { Length: > 0 } tags
            ? [.. tags]
            : Array.Empty<string>();
        target.AllowAnonymous = source.AllowAnonymous;
        target.TreatDegradedAsUnhealthy = source.TreatDegradedAsUnhealthy;
        target.ThrowOnDuplicate = source.ThrowOnDuplicate;
        target.RequireSchemes = source.RequireSchemes is { Length: > 0 } schemes
            ? [.. schemes]
            : Array.Empty<string>();
        target.RequirePolicies = source.RequirePolicies is { Length: > 0 } policies
            ? [.. policies]
            : Array.Empty<string>();
        target.CorsPolicyName = source.CorsPolicyName;
        target.RateLimitPolicyName = source.RateLimitPolicyName;
        target.ShortCircuit = source.ShortCircuit;
        target.ShortCircuitStatusCode = source.ShortCircuitStatusCode;
        target.OpenApiSummary = source.OpenApiSummary;
        target.OpenApiDescription = source.OpenApiDescription;
        target.OpenApiOperationId = source.OpenApiOperationId;
        target.OpenApiTags = source.OpenApiTags is { Length: > 0 } openApiTags
            ? [.. openApiTags]
            : Array.Empty<string>();
        target.OpenApiGroupName = source.OpenApiGroupName;
        target.MaxDegreeOfParallelism = source.MaxDegreeOfParallelism;
        target.ProbeTimeout = source.ProbeTimeout;
        target.AutoRegisterEndpoint = source.AutoRegisterEndpoint;
        target.DefaultScriptLanguage = source.DefaultScriptLanguage;
        // BUGFIX: Ensure the response content type preference is propagated when using the overload
        // that accepts a pre-configured HealthEndpointOptions instance. Without this line the
        // ResponseContentType would always fall back to Json for PowerShell Add-KrHealthEndpoint
        // which calls AddHealthEndpoint(host, options) internally.
        target.ResponseContentType = source.ResponseContentType;
        target.XmlRootElementName = source.XmlRootElementName;
        target.Compress = source.Compress;
    }
    private static int DetermineStatusCode(ProbeStatus status, bool treatDegradedAsUnhealthy) => status switch
    {
        ProbeStatus.Healthy => StatusCodes.Status200OK,
        ProbeStatus.Degraded when !treatDegradedAsUnhealthy => StatusCodes.Status200OK,
        _ => StatusCodes.Status503ServiceUnavailable
    };

    private static void MapHealthEndpointImmediate(KestrunHost host, HealthEndpointOptions merged, MapRouteOptions mapOptions)
    {
        if (host.MapExists(mapOptions.Pattern!, HttpVerb.Get))
        {
            var message = $"Route '{mapOptions.Pattern}' (GET) already exists. Skipping health endpoint registration.";
            if (merged.ThrowOnDuplicate)
            {
                throw new InvalidOperationException(message);
            }
            host.HostLogger.Warning(message);
            return;
        }

        // Acquire WebApplication (throws if Build() truly has not executed yet). Using host.App here allows
        // early AddHealthEndpoint calls before EnableConfiguration via deferred middleware.
        var endpoints = host.App;
        var endpointLogger = host.HostLogger.ForContext("HealthEndpoint", merged.Pattern);

        var map = endpoints.MapMethods(merged.Pattern, [HttpMethods.Get], async context =>
        {
            var requestTags = ExtractTags(context.Request);
            var tags = requestTags.Length > 0 ? requestTags : merged.DefaultTags;
            var snapshot = host.GetHealthProbesSnapshot();

            var report = await HealthProbeRunner.RunAsync(
                probes: snapshot,
                tagFilter: tags,
                perProbeTimeout: merged.ProbeTimeout,
                maxDegreeOfParallelism: merged.MaxDegreeOfParallelism,
                logger: endpointLogger,
                ct: context.RequestAborted).ConfigureAwait(false);

            var request = await KestrunRequest.NewRequest(context).ConfigureAwait(false);
            var response = new KestrunResponse(request)
            {
                CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MustRevalidate = true,
                    MaxAge = TimeSpan.Zero
                }
            };
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";

            var statusCode = DetermineStatusCode(report.Status, merged.TreatDegradedAsUnhealthy);
            switch (merged.ResponseContentType)
            {
                case HealthEndpointContentType.Json:
                    await response.WriteJsonResponseAsync(report, depth: 10, compress: merged.Compress, statusCode: statusCode).ConfigureAwait(false);
                    break;
                case HealthEndpointContentType.Yaml:
                    await response.WriteYamlResponseAsync(report, statusCode).ConfigureAwait(false);
                    break;
                case HealthEndpointContentType.Xml:
                    await response.WriteXmlResponseAsync(
                        report,
                        statusCode,
                        rootElementName: merged.XmlRootElementName ?? "Response",
                        compress: merged.Compress).ConfigureAwait(false);
                    break;
                case HealthEndpointContentType.Text:
                    var text = HealthReportTextFormatter.Format(report);
                    await response.WriteTextResponseAsync(text, statusCode, contentType: $"text/plain; charset={response.Encoding.WebName}").ConfigureAwait(false);
                    break;
                case HealthEndpointContentType.Auto:
                default:
                    await response.WriteResponseAsync(report, statusCode).ConfigureAwait(false);
                    break;
            }

            await response.ApplyTo(context.Response).ConfigureAwait(false);
        }).WithMetadata(new ScriptLanguageAttribute(ScriptLanguage.Native));

        host.AddMapOptions(map, mapOptions);
        host._registeredRoutes[(mapOptions.Pattern!, HttpMethods.Get)] = mapOptions;
        host.HostLogger.Information("Registered health endpoint at {Pattern}", mapOptions.Pattern);
    }
}
