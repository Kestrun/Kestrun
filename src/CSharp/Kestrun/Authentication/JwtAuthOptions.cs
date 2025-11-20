using Kestrun.Claims;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Kestrun.Authentication;

/// <summary>
/// Options for JWT-based authentication.
/// </summary>
public class JwtAuthOptions : JwtBearerOptions, IOpenApiAuthenticationOptions, IAuthenticationHostOptions
{
    /// <summary>
    /// If true, allows cookie authentication over insecure HTTP connections.
    /// </summary>
    public bool AllowInsecureHttp { get; set; }

    /// <inheritdoc/>
    public string? DisplayName { get; set; }

    /// <inheritdoc/>
    public bool GlobalScheme { get; set; }

    /// <inheritdoc/>
    public string? Description { get; set; }

    /// <inheritdoc/>
    public string[] DocumentationId { get; set; } = [];

    /// <inheritdoc/>
    public KestrunHost Host { get; set; } = default!;

    /// <inheritdoc/>
    public Serilog.ILogger Logger => Host.Logger;

    /// <summary>
    /// Configuration for claim policy enforcement.
    /// </summary>
    public ClaimPolicyConfig? ClaimPolicy { get; set; }

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
