using Kestrun.Scripting;

namespace Kestrun.Health;

/// <summary>
/// Options controlling the built-in health endpoint exposed by <see cref="Hosting.KestrunHost"/>.
/// </summary>
public sealed class HealthEndpointOptions
{
#pragma warning disable IDE0032 // Use auto property
    private string _pattern = "/health";
#pragma warning restore IDE0032 // Use auto property

    /// <summary>
    /// Gets or sets the relative route path the endpoint is exposed on. Defaults to <c>/health</c>.
    /// </summary>
    public string Pattern
    {
        get => _pattern;
        set => _pattern = string.IsNullOrWhiteSpace(value) ? "/health" : (value.StartsWith('/') ? value : "/" + value);
    }

    /// <summary>
    /// Gets or sets the default probe tags applied when a request does not provide an explicit tag filter.
    /// </summary>
    public string[] DefaultTags { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether anonymous callers can hit the endpoint.
    /// </summary>
    public bool AllowAnonymous { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether degraded probes should cause the endpoint to return <c>503 ServiceUnavailable</c>.
    /// </summary>
    public bool TreatDegradedAsUnhealthy { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an exception should be thrown if an endpoint with the same pattern already exists.
    /// </summary>
    public bool ThrowOnDuplicate { get; set; }

    /// <summary>
    /// Gets or sets additional authentication schemes required for the endpoint. Leave empty to inherit the application's defaults.
    /// </summary>
    public string[] RequireSchemes { get; set; } = [];

    /// <summary>
    /// Gets or sets additional authorization policies required for the endpoint.
    /// </summary>
    public string[] RequirePolicies { get; set; } = [];

    /// <summary>
    /// Gets or sets the name of a CORS policy to apply, if any.
    /// </summary>
    public string? CorsPolicyName { get; set; }

    /// <summary>
    /// Gets or sets the name of an ASP.NET Core rate limiting policy to apply, if any.
    /// </summary>
    public string? RateLimitPolicyName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the endpoint should short-circuit the rest of the pipeline.
    /// </summary>
    public bool ShortCircuit { get; set; }

    /// <summary>
    /// Gets or sets the status code returned when <see cref="ShortCircuit"/> is <c>true</c>. Defaults to <c>200</c> or <c>503</c> depending on probe state.
    /// </summary>
    public int? ShortCircuitStatusCode { get; set; }

    /// <summary>
    /// Gets or sets the OpenAPI summary applied to the endpoint metadata.
    /// </summary>
    public string? OpenApiSummary { get; set; } = "Aggregate health status.";

    /// <summary>
    /// Gets or sets the OpenAPI description applied to the endpoint metadata.
    /// </summary>
    public string? OpenApiDescription { get; set; } = "Returns the current reported state of all registered probes.";

    /// <summary>
    /// Gets or sets the OpenAPI operation id applied to the endpoint metadata.
    /// </summary>
    public string? OpenApiOperationId { get; set; } = "GetHealth";

    /// <summary>
    /// Gets or sets the OpenAPI tag list applied to the endpoint metadata.
    /// </summary>
    public string[] OpenApiTags { get; set; } = ["Health"];

    /// <summary>
    /// Gets or sets the OpenAPI group name applied to the endpoint metadata.
    /// </summary>
    public string? OpenApiGroupName { get; set; }

    /// <summary>
    /// Gets or sets the maximum degree of parallelism used when executing probes.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets the timeout applied to each individual probe execution.
    /// </summary>
    public TimeSpan ProbeTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets a value indicating whether the endpoint should be automatically registered when a host is created.
    /// </summary>
    public bool AutoRegisterEndpoint { get; set; } = true;

    /// <summary>
    /// Gets or sets the script language used when generating health check probes from script. Defaults to <see cref="ScriptLanguage.PowerShell"/>.
    /// </summary>
    public ScriptLanguage DefaultScriptLanguage { get; set; } = ScriptLanguage.PowerShell;

    /// <summary>
    /// Creates a deep copy of the current instance.
    /// </summary>
    /// <returns>A cloned <see cref="HealthEndpointOptions"/> instance.</returns>
    public HealthEndpointOptions Clone() => new()
    {
        Pattern = Pattern,
        DefaultTags = [.. DefaultTags],
        AllowAnonymous = AllowAnonymous,
        TreatDegradedAsUnhealthy = TreatDegradedAsUnhealthy,
        ThrowOnDuplicate = ThrowOnDuplicate,
        RequireSchemes = [.. RequireSchemes],
        RequirePolicies = [.. RequirePolicies],
        CorsPolicyName = CorsPolicyName,
        RateLimitPolicyName = RateLimitPolicyName,
        ShortCircuit = ShortCircuit,
        ShortCircuitStatusCode = ShortCircuitStatusCode,
        OpenApiSummary = OpenApiSummary,
        OpenApiDescription = OpenApiDescription,
        OpenApiOperationId = OpenApiOperationId,
        OpenApiTags = [.. OpenApiTags],
        OpenApiGroupName = OpenApiGroupName,
        MaxDegreeOfParallelism = MaxDegreeOfParallelism,
        ProbeTimeout = ProbeTimeout,
        AutoRegisterEndpoint = AutoRegisterEndpoint,
        DefaultScriptLanguage = DefaultScriptLanguage
    };
}
