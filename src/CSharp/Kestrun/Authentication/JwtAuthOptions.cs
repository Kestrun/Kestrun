using System.Security.Claims;
using Kestrun.Claims;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Kestrun.Authentication;

/// <summary>
/// Options for JWT-based authentication.
/// </summary>
public class JwtAuthOptions : JwtBearerOptions, IOpenApiAuthenticationOptions, IClaimsCommonOptions, IAuthenticationHostOptions
{
    /// <summary>
    /// If true, allows cookie authentication over insecure HTTP connections.
    /// </summary>
    public bool AllowInsecureHttp { get; set; }

    private Serilog.ILogger? _logger;
    /// <inheritdoc/>
    public Serilog.ILogger Logger
    {
        get => _logger ?? (Host is null ? Serilog.Log.Logger : Host.Logger); set => _logger = value;
    }

    /// <summary>
    /// Gets or sets the token validation parameters.
    /// </summary>
    public TokenValidationParameters? ValidationParameters { get; set; }

    /// <inheritdoc/>
    public string? DisplayName { get; set; }

    /// <inheritdoc/>
    public bool GlobalScheme { get; set; }

    /// <inheritdoc/>
    public string? Description { get; set; }

    /// <inheritdoc/>
    public string[] DocumentationId { get; set; } = [];

      /// <inheritdoc/>
    public bool Deprecated { get; set; }

    /// <inheritdoc/>
    public KestrunHost Host { get; set; } = default!;

    /// <summary>
    /// Configuration for claim policy enforcement.
    /// </summary>
    public ClaimPolicyConfig? ClaimPolicy { get; set; }

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

    /// <summary>
    /// Helper to copy values from a user-supplied JwtBearerOptions instance to the instance
    /// created by the framework inside AddJwtBearer(). Reassigning the local variable (opts = source) would
    /// not work because only the local reference changes – the framework keeps the original instance.
    /// </summary>
    /// <param name="target">The target options to copy to.</param>
    /// <exception cref="ArgumentNullException">Thrown when source or target is null.</exception>
    /// <remarks>
    /// Only copies primitive properties and references. Does not clone complex objects like CookieBuilder.
    /// </remarks>
    public void ApplyTo(JwtAuthOptions target)
    {
        ApplyTo((JwtBearerOptions)target);
        target.GlobalScheme = GlobalScheme;
        target.Description = Description;
        target.DocumentationId = DocumentationId;
        target.DisplayName = DisplayName;
        target.Host = Host;
        target.ClaimPolicy = ClaimPolicy;
        target.IssueClaims = IssueClaims;
        target.IssueClaimsCodeSettings = IssueClaimsCodeSettings;
        target.Deprecated = Deprecated;
    }

    /// <summary>
    /// Helper to copy values from this JwtAuthOptions instance to a target JwtBearerOptions instance.
    /// </summary>
    /// <param name="target">The target JwtBearerOptions instance to copy values to.</param>
    public void ApplyTo(JwtBearerOptions target)
    {
        // Paths & return URL
        target.TokenValidationParameters = TokenValidationParameters;
        target.MapInboundClaims = MapInboundClaims;
        target.SaveToken = SaveToken;
        target.IncludeErrorDetails = IncludeErrorDetails;
        target.RefreshOnIssuerKeyNotFound = RefreshOnIssuerKeyNotFound;
        target.RequireHttpsMetadata = RequireHttpsMetadata;
        target.MetadataAddress = MetadataAddress;
        target.Authority = Authority;
        target.Audience = Audience;
        target.Challenge = Challenge;
    }
}
