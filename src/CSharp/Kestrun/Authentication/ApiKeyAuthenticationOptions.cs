using System.Security.Claims;
using System.Text;
using Kestrun.Claims;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;

namespace Kestrun.Authentication;

/// <summary>
/// Options for API key authentication, including header names, validation, and claims issuance.
/// </summary>
public class ApiKeyAuthenticationOptions() : AuthenticationSchemeOptions, IAuthenticationCommonOptions, IAuthenticationHostOptions, IOpenApiAuthenticationOptions
{
    /// <summary>
    /// Name of to look for the API key.
    /// </summary>
    public string ApiKeyName { get; set; } = "X-Api-Key";

    /// <summary>
    /// Location to look for the API key.
    /// </summary>
    public ParameterLocation In { get; set; } = ParameterLocation.Header;

    /// <summary>
    /// Other headers to try if the primary one is missing.
    /// <para>Defaults to empty.</para>
    /// <para>Use this to support multiple header names for the API key.</para>
    /// </summary>
    public string[] AdditionalHeaderNames { get; set; } = [];

    /// <summary>
    /// If true, also look for the key in the query string.
    /// <para>Defaults to false.</para>
    /// <para>Note: this is less secure, as query strings can be logged.</para>
    /// <para>Use with caution.</para>
    /// </summary>
    public bool AllowQueryStringFallback { get; set; }

    /// <summary>
    /// Single expected API key (used if ValidateKey is not set).
    /// <para>Defaults to null.</para>
    /// <para>Use this for simple scenarios where you have a known key.</para>
    /// </summary>
    public string? StaticApiKey { get; set; }

    /// <summary>
    /// Gets the expected API key as a UTF-8 byte array, or null if <see cref="StaticApiKey"/> is not set.
    /// </summary>
    public byte[]? StaticApiKeyAsBytes => StaticApiKey is not null ? Encoding.UTF8.GetBytes(StaticApiKey) : null;

    /// <summary>
    /// If true, allows API key authentication over insecure HTTP connections.
    /// </summary>
    public bool AllowInsecureHttp { get; set; }

    /// <summary>
    /// If true, includes the <c>WWW-Authenticate</c> header in 401 responses.
    /// <para>Default: <c>true</c>.</para>
    /// <para>Set to <c>false</c> to suppress automatic hints to clients.</para>
    /// </summary>
    public bool EmitChallengeHeader { get; set; } = true;

    /// <summary>
    /// Format for the <c>WWW-Authenticate</c> header in 401 responses.
    /// <para>
    /// If set to <c>ApiKeyHeader</c>, emits <c>ApiKey header="X-Api-Key"</c>.
    /// If set to <c>HeaderOnly</c>, emits just the header name.
    /// </para>
    /// </summary>
    public ApiKeyChallengeFormat ChallengeHeaderFormat { get; set; } = ApiKeyChallengeFormat.ApiKeyHeader;

    /// <summary>
    /// Called to validate the raw key string. Return true if valid.
    /// <para>This is called for every request, so it should be fast.</para>
    /// </summary>
    public Func<HttpContext, string, byte[], Task<bool>> ValidateKeyAsync { get; set; } = (_, _, _) => Task.FromResult(false);

    /// <summary>
    /// Settings for the authentication code, if using a script.
    /// </summary>
    /// <remarks>
    /// This allows you to specify the language, code, and additional imports/refs.
    /// </remarks>
    public AuthenticationCodeSettings ValidateCodeSettings { get; set; } = new();

    /// <summary>
    /// After credentials are valid, this is called to add extra Claims.
    /// Parameters: HttpContext, username → IEnumerable of extra claims.
    /// </summary>
    public Func<HttpContext, string, Task<IEnumerable<Claim>>>? IssueClaims { get; set; }

    /// <summary>
    /// Settings for the claims issuing code, if using a script.
    /// </summary>
    /// <remarks>
    /// This allows you to specify the language, code, and additional imports/refs for claims issuance.
    /// </remarks>
    public AuthenticationCodeSettings IssueClaimsCodeSettings { get; set; } = new();

    /// <summary>
    /// Gets or sets the claim policy configuration.
    /// </summary>
    /// <remarks>
    /// This allows you to define multiple authorization policies based on claims.
    /// Each policy can specify a claim type and allowed values.
    /// </remarks>
    public ClaimPolicyConfig? ClaimPolicyConfig { get; set; }

    /// <inheritdoc/>
    public bool GlobalScheme { get; set; }

    /// <inheritdoc/>
    public string? Description { get; set; }

    /// <inheritdoc/>
    public string[] DocumentationId { get; set; } = [];

    /// <inheritdoc/>
    public KestrunHost Host { get; set; } = default!;

    /// <inheritdoc/>
    public Serilog.ILogger Logger => Host?.Logger ?? Serilog.Log.Logger;


    /// <summary>
    /// Helper to copy values from a user-supplied ApiKeyAuthenticationOptions instance to the instance
    /// created by the framework inside AddApiKey(). Reassigning the local variable (opts = source) would
    /// not work because only the local reference changes – the framework keeps the original instance
    /// </summary>
    /// <param name="target">The target instance to which values will be copied. </param>
    public void ApplyTo(ApiKeyAuthenticationOptions target)
    {
        // Copy base AuthenticationSchemeOptions properties
        target.ClaimsIssuer = ClaimsIssuer;
        target.EventsType = EventsType;
        target.Events = Events;
        target.ApiKeyName = ApiKeyName;
        target.In = In;
        target.AdditionalHeaderNames = AdditionalHeaderNames;
        target.AllowQueryStringFallback = AllowQueryStringFallback;
        target.StaticApiKey = StaticApiKey;
        target.AllowInsecureHttp = AllowInsecureHttp;
        target.EmitChallengeHeader = EmitChallengeHeader;
        target.ChallengeHeaderFormat = ChallengeHeaderFormat;
        target.ValidateKeyAsync = ValidateKeyAsync;
        target.ValidateCodeSettings = ValidateCodeSettings;
        target.IssueClaims = IssueClaims;
        target.IssueClaimsCodeSettings = IssueClaimsCodeSettings;
        target.ClaimPolicyConfig = ClaimPolicyConfig;

        // Copy IAuthenticationHostOptions properties
        target.Host = Host;
        // OpenAPI / documentation properties(IOpenApiAuthenticationOptions)
        target.GlobalScheme = GlobalScheme;
        target.Description = Description;
        target.DocumentationId = DocumentationId;
    }
}
