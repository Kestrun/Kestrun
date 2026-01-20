using Kestrun.Health;
using Kestrun.Hosting.Options;
using Kestrun.Models;
using Kestrun.Scripting;
using Kestrun.Utilities;
using Microsoft.Net.Http.Headers;

namespace Kestrun.Hosting;

/// <summary>
/// Adds health-check specific helpers to <see cref="KestrunHost"/>.
/// </summary>
public partial class KestrunHost
{
    /// <summary>
    /// Registers a GET endpoint (default <c>/health</c>) that aggregates the state of all registered probes.
    /// </summary>
    /// <param name="configure">Optional action to mutate the default endpoint options.</param>
    /// <returns>The <see cref="KestrunHost"/> instance for fluent chaining.</returns>
    public KestrunHost AddHealthEndpoint(Action<HealthEndpointOptions>? configure = null)
    {
        var merged = Options.Health?.Clone() ?? new HealthEndpointOptions();
        configure?.Invoke(merged);

        var mapOptions = new MapRouteOptions
        {
            Pattern = merged.Pattern,
            HttpVerbs = [HttpVerb.Get],
            ScriptCode = new LanguageOptions
            {
                Language = ScriptLanguage.Native,
            },
            AllowAnonymous = merged.AllowAnonymous,
            DisableAntiforgery = true,
            RequireSchemes = [.. merged.RequireSchemes],
            RequirePolicies = [.. merged.RequirePolicies],
            CorsPolicy = merged.CorsPolicy ?? string.Empty,
            RateLimitPolicyName = merged.RateLimitPolicyName,
            ShortCircuit = merged.ShortCircuit,
            ShortCircuitStatusCode = merged.ShortCircuitStatusCode,
            ThrowOnDuplicate = merged.ThrowOnDuplicate,
        };
        mapOptions.OpenAPI.Add(HttpVerb.Get, new OpenAPIPathMetadata(pattern: merged.Pattern, mapOptions: mapOptions)
        {
            Summary = merged.OpenApiSummary,
            Description = merged.OpenApiDescription,
            OperationId = merged.OpenApiOperationId,
            Tags = merged.OpenApiTags
        });

        // Auto-register endpoint only when enabled
        if (!merged.AutoRegisterEndpoint)
        {
            Logger.Debug("Health endpoint AutoRegisterEndpoint=false; skipping automatic mapping for pattern {Pattern}", merged.Pattern);
            return this;
        }

        // If the app pipeline is already built/configured, map immediately; otherwise defer until build
        if (IsConfigured)
        {
            MapHealthEndpointImmediate(merged, mapOptions);
            return this;
        }

        return Use(app => MapHealthEndpointImmediate(merged, mapOptions));
    }

    /// <summary>
    /// Registers a GET endpoint (default <c>/health</c>) using a pre-configured <see cref="HealthEndpointOptions"/> instance.
    /// </summary>
    /// <param name="options">A fully configured options object.</param>
    /// <returns>The <see cref="KestrunHost"/> instance for fluent chaining.</returns>
    public KestrunHost AddHealthEndpoint(HealthEndpointOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return AddHealthEndpoint(dest => CopyHealthEndpointOptions(options, dest));
    }

    /// <summary>
    /// Extracts tags from the HTTP request query parameters.
    /// </summary>
    /// <param name="request">The HTTP request containing query parameters.</param>
    /// <returns>An array of extracted tags.</returns>
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

    /// <summary>
    /// Copies health endpoint options from source to target.
    /// </summary>
    /// <param name="source">The source HealthEndpointOptions instance.</param>
    /// <param name="target">The target HealthEndpointOptions instance.</param>
    private static void CopyHealthEndpointOptions(HealthEndpointOptions source, HealthEndpointOptions target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        target.Pattern = source.Pattern;
        target.DefaultTags = source.DefaultTags is { Length: > 0 } tags
            ? [.. tags]
            : [];
        target.AllowAnonymous = source.AllowAnonymous;
        target.TreatDegradedAsUnhealthy = source.TreatDegradedAsUnhealthy;
        target.ThrowOnDuplicate = source.ThrowOnDuplicate;
        target.RequireSchemes = source.RequireSchemes is { Length: > 0 } schemes
            ? [.. schemes]
            : [];
        target.RequirePolicies = source.RequirePolicies is { Length: > 0 } policies
            ? [.. policies]
            : [];
        target.CorsPolicy = source.CorsPolicy;
        target.RateLimitPolicyName = source.RateLimitPolicyName;
        target.ShortCircuit = source.ShortCircuit;
        target.ShortCircuitStatusCode = source.ShortCircuitStatusCode;
        target.OpenApiSummary = source.OpenApiSummary;
        target.OpenApiDescription = source.OpenApiDescription;
        target.OpenApiOperationId = source.OpenApiOperationId;
        target.OpenApiTags = source.OpenApiTags is { Count: > 0 } openApiTags
            ? [.. openApiTags]
            : [];
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

    /// <summary>
    /// Maps the health endpoint immediately.
    /// </summary>
    /// <param name="merged">The merged HealthEndpointOptions instance.</param>
    /// <param name="mapOptions">The route mapping options.</param>
    /// <exception cref="InvalidOperationException">Thrown if a route with the same pattern and HTTP verb already exists and ThrowOnDuplicate is true.</exception>
    private void MapHealthEndpointImmediate(HealthEndpointOptions merged, MapRouteOptions mapOptions)
    {
        if (this.MapExists(mapOptions.Pattern!, HttpVerb.Get))
        {
            var message = $"Route '{mapOptions.Pattern}' (GET) already exists. Skipping health endpoint registration.";
            if (merged.ThrowOnDuplicate)
            {
                throw new InvalidOperationException(message);
            }
            Logger.Warning(message);
            return;
        }

        // Acquire WebApplication (throws if Build() truly has not executed yet). Using App here allows
        // early AddHealthEndpoint calls before EnableConfiguration via deferred middleware.
        var endpoints = App;
        var endpointLogger = Logger.ForContext("HealthEndpoint", merged.Pattern);

        var map = endpoints.MapMethods(merged.Pattern, [HttpMethods.Get], async context =>
        {
            var requestTags = ExtractTags(context.Request);
            var tags = requestTags.Length > 0 ? requestTags : merged.DefaultTags;
            var snapshot = GetHealthProbesSnapshot();

            var report = await HealthProbeRunner.RunAsync(
                probes: snapshot,
                tagFilter: tags,
                perProbeTimeout: merged.ProbeTimeout,
                maxDegreeOfParallelism: merged.MaxDegreeOfParallelism,
                logger: endpointLogger,
                ct: context.RequestAborted).ConfigureAwait(false);

            var krContext = new KestrunContext(this, context);
            var response = krContext.Response;
            response.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true,
                MustRevalidate = true,
                MaxAge = TimeSpan.Zero
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

        this.AddMapOptions(map, mapOptions);
        _registeredRoutes[(mapOptions.Pattern!, HttpVerb.Get)] = mapOptions;
        Logger.Information("Registered health endpoint at {Pattern}", mapOptions.Pattern);
    }
}
