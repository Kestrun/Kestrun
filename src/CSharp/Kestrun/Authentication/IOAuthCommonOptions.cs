using Kestrun.Claims;

namespace Kestrun.Authentication;

/// <summary>
/// Common options for OAuth and OpenID Connect authentication.
/// </summary>
internal interface IOAuthCommonOptions
{
    /// <summary>
    /// Gets or sets the claim policy configuration.
    /// </summary>
    ClaimPolicyConfig? ClaimPolicy { get; set; }

    /// <summary>
    /// Gets the scopes requested during authentication.
    /// </summary>
    ICollection<string> Scope { get; }
}
