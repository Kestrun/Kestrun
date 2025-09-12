using System.Security.Claims;

namespace Kestrun.Authorization;

/// <summary>
/// Configuration for a named authorization policy.
/// </summary>
public sealed class KestrunPolicyConfig
{
    /// <summary>
    /// The name of the policy.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// If set, the policy will require these roles.
    /// </summary>
    public string[]? RequiredRoles { get; set; }

    /// <summary>
    /// If set, the policy will require these claims.
    /// </summary>
    public Claim[]? RequiredClaims { get; set; }

    /// <summary>
    /// If set, the policy will use these authentication schemes.
    /// </summary>
    public string[]? AuthenticationSchemes { get; set; }
}
