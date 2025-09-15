


using System.Security.Claims;
using Kestrun.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Kestrun.Authentication;


/// <summary>
/// Provides options for JWT authentication, including claim policies and custom claim issuance.
/// </summary>
public class JwtAuthenticationOptions : JwtBearerOptions, IClaimsCommonOptions
{
    /// <summary>
    /// Gets or sets the token validation parameters.
    /// </summary>
    public TokenValidationParameters? ValidationParameters { get; set; }

    /// <summary>
    /// Gets or sets the claim policy.
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
}
