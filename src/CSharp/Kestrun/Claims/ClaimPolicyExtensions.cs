using Microsoft.AspNetCore.Authorization;

namespace Kestrun.Claims;


/// <summary>
/// Extension methods for converting ClaimPolicyConfig into ASP.NET Core authorization setup.
/// </summary>
public static class ClaimPolicyExtensions
{
    /// <summary>
    /// Turns <see cref="ClaimPolicyConfig"/> into the delegate that
    /// <c>services.AddAuthorization(...)</c> needs.
    /// </summary>
    /// <param name="cfg">The claim-based policy configuration.</param>
    /// <returns>An <see cref="Action{AuthorizationOptions}"/> that registers all policies.</returns>
    public static Action<AuthorizationOptions> ToAuthzDelegate(this ClaimPolicyConfig cfg)
        => options =>
        {
            foreach (var (name, rule) in cfg.Policies)
            {
                options.AddPolicy(name, p =>
                    p.RequireClaim(rule.ClaimType, rule.AllowedValues));
            }
        };
}
