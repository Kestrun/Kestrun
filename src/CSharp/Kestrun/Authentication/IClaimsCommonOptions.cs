using System.Security.Claims;
using Kestrun.Claims;

namespace Kestrun.Authentication;


/// <summary>
/// Defines common options for authentication, including code validation, claim issuance, and claim policy configuration.
/// </summary>
public interface IClaimsCommonOptions
{
    /// <summary>
    /// After credentials are valid, this is called to add extra Claims.
    /// Parameters: HttpContext, username → IEnumerable of extra claims.
    /// </summary>
    Func<HttpContext, string, Task<IEnumerable<Claim>>>? IssueClaims { get; set; }
    /// <summary>
    /// Settings for the claims issuing code, if using a script.
    /// </summary>
    /// <remarks>
    /// This allows you to specify the language, code, and additional imports/refs for claims issuance.
    /// </remarks>
    AuthenticationCodeSettings IssueClaimsCodeSettings { get; set; }

    /// <summary>
    /// Gets or sets the claim policy configuration.
    /// </summary>
    /// <remarks>
    /// This allows you to define multiple authorization policies based on claims.
    /// Each policy can specify a claim type and allowed values.
    /// </remarks>
    ClaimPolicyConfig? ClaimPolicyConfig { get; set; }
}
