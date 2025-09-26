using Kestrun.Health;
using Kestrun.Hosting.Options;
using Kestrun.Scripting;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Authorization;
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

    private static void ApplyConventions(KestrunHost host, IEndpointConventionBuilder map, MapRouteOptions options)
    {
        if (options.AllowAnonymous)
        {
            _ = map.AllowAnonymous();
        }

        if (options.DisableAntiforgery)
        {
            _ = map.DisableAntiforgery();
        }

        if (!string.IsNullOrWhiteSpace(options.CorsPolicyName))
        {
            _ = map.RequireCors(options.CorsPolicyName);
        }

        if (!string.IsNullOrWhiteSpace(options.RateLimitPolicyName))
        {
            _ = map.RequireRateLimiting(options.RateLimitPolicyName);
        }

        if (options.RequireSchemes is { Length: > 0 })
        {
            foreach (var scheme in options.RequireSchemes)
            {
                if (!host.HasAuthScheme(scheme))
                {
                    throw new ArgumentException($"Authentication scheme '{scheme}' is not registered.", nameof(options.RequireSchemes));
                }
            }

            _ = map.RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = string.Join(',', options.RequireSchemes)
            });
        }

        if (options.RequirePolicies is { Length: > 0 })
        {
            foreach (var policy in options.RequirePolicies)
            {
                if (!host.HasAuthPolicy(policy))
                {
                    throw new ArgumentException($"Authorization policy '{policy}' is not registered.", nameof(options.RequirePolicies));
                }
            }

            _ = map.RequireAuthorization(options.RequirePolicies);
        }

        if (options.ShortCircuit)
        {
            _ = map.ShortCircuit(options.ShortCircuitStatusCode ?? StatusCodes.Status200OK);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.OperationId))
        {
            _ = map.WithName(options.OpenAPI.OperationId);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.Summary))
        {
            _ = map.WithSummary(options.OpenAPI.Summary);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.Description))
        {
            _ = map.WithDescription(options.OpenAPI.Description);
        }

        if (options.OpenAPI.Tags is { Length: > 0 })
        {
            _ = map.WithTags(options.OpenAPI.Tags);
        }

        if (!string.IsNullOrWhiteSpace(options.OpenAPI.GroupName))
        {
            _ = map.WithGroupName(options.OpenAPI.GroupName);
        }
    }

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

        // When immediate mapping after build ensure underlying WebApplication is available
        var endpoints = host.IsConfigured ? host.App : null;
        if (endpoints is null)
        {
            // We are in a deferred Use callback (app passed later)
            throw new InvalidOperationException("Endpoint mapping requires a WebApplication when executed immediately.");
        }
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

            var statusCode = DetermineStatusCode(report.Status, merged.TreatDegradedAsUnhealthy);
            context.Response.StatusCode = statusCode;
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";

            await context.Response.WriteAsJsonAsync(report, JsonOptions, context.RequestAborted).ConfigureAwait(false);
        }).WithMetadata(new ScriptLanguageAttribute(ScriptLanguage.Native));

        ApplyConventions(host, map, mapOptions);
        host._registeredRoutes[(mapOptions.Pattern!, HttpMethods.Get)] = mapOptions;
        host.HostLogger.Information("Registered health endpoint at {Pattern}", mapOptions.Pattern);
    }
}
