using Kestrun.Utilities;

namespace Kestrun.Hosting.Options;
/// <summary>
/// Options for mapping a route, including pattern, HTTP verbs, script code, authorization, and metadata.
/// </summary>
public record MapRouteOptions
{
    /// <summary>
    /// The route pattern to match for this option.
    /// </summary>
    public string? Pattern { get; set; }
    /// <summary>
    /// The HTTP verbs (methods) that this route responds to.
    /// </summary>
    public List<HttpVerb> HttpVerbs { get; set; } = [];
    /// <summary>
    /// Authorization Scheme names required for this route.
    /// </summary>
    public string[] RequireSchemes { get; set; } = []; // Authorization scheme name, if any
    /// <summary>
    /// Authorization policy names required for this route.
    /// </summary>
    public string[]? RequirePolicies { get; set; } = []; // Authorization policies, if any
    /// <summary>
    /// Name of the CORS policy to apply, if any.
    /// </summary>
    public string CorsPolicyName { get; set; } = string.Empty; // Name of the CORS policy to apply, if any
    /// <summary>
    /// If true, short-circuits the pipeline after this route.
    /// </summary>
    public bool ShortCircuit { get; set; } // If true, short-circuit the pipeline after this route
    /// <summary>
    /// Status code to return if short-circuiting the pipeline after this route.
    /// </summary>
    public int? ShortCircuitStatusCode { get; set; } = null; // Status code to return if short-circuiting
    /// <summary>
    /// If true, allows anonymous access to this route.
    /// </summary>
    public bool AllowAnonymous { get; set; }
    /// <summary>
    /// If true, disables antiforgery protection for this route.
    /// </summary>
    public bool DisableAntiforgery { get; set; }
    /// <summary>
    /// If true, disables response compression for this route.
    /// </summary>
    public bool DisableResponseCompression { get; set; }
    /// <summary>
    /// The name of the rate limit policy to apply to this route, if any.
    /// </summary>
    public string? RateLimitPolicyName { get; set; }
    /// <summary>
    /// Endpoints to bind the route to, if any.
    /// </summary>
    public string[]? Endpoints { get; set; } = [];

    /// <summary>
    /// OpenAPI metadata for this route.
    /// </summary>
    public OpenAPIMetadata OpenAPI { get; set; } = new OpenAPIMetadata(); // OpenAPI metadata for this route

    /// <summary>
    /// Script code and language options for this route.
    /// </summary>
    public LanguageOptions ScriptCode { get; init; } = new LanguageOptions();
    /// <summary>
    /// If true, throws an exception on duplicate routes.
    /// </summary>
    public bool ThrowOnDuplicate { get; set; }
    /// <summary>
    /// Returns a string representation of the MapRouteOptions.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        var verbs = HttpVerbs.Count > 0 ? string.Join(",", HttpVerbs) : "ANY";
        return $"{verbs} {Pattern}";
    }
}
