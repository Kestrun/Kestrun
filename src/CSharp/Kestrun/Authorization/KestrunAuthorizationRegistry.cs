// Kestrun.Authorization.KestrunAuthorizationRegistry.cs
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;

namespace Kestrun.Authorization;

/// <summary>
/// Registry for Kestrun authorization policies.
/// </summary>
public static class KestrunAuthorizationRegistry
{
    private static readonly ConcurrentDictionary<string, AuthorizationPolicy> _policies =
        new(StringComparer.OrdinalIgnoreCase);

    private static volatile AuthorizationPolicy? _defaultPolicy;
    private static volatile AuthorizationPolicy? _fallbackPolicy;

    /// <summary>
    /// Adds or updates a named authorization policy.
    /// </summary>
    /// <param name="cfg">The policy configuration.</param>
    /// <exception cref="ArgumentException">Thrown when the policy name is empty.</exception>
    public static void AddOrUpdatePolicy(KestrunPolicyConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.Name))
        {
            throw new ArgumentException("Policy Name cannot be empty.", nameof(cfg));
        }

        var b = new AuthorizationPolicyBuilder();
        if (cfg.AuthenticationSchemes is { Length: > 0 })
        {
            _ = b.AddAuthenticationSchemes(cfg.AuthenticationSchemes);
        }

        if (cfg.RequiredRoles is { Length: > 0 })
        {
            _ = b.RequireRole(cfg.RequiredRoles);
        }

        if (cfg.RequiredClaims is { Length: > 0 })
        {
            foreach (var group in cfg.RequiredClaims
                         .Where(c => c is not null && !string.IsNullOrWhiteSpace(c.Type))
                         .GroupBy(c => c!.Type, StringComparer.Ordinal))
            {
                var values = group.Select(c => c!.Value).Where(v => !string.IsNullOrEmpty(v))
                                  .Distinct(StringComparer.Ordinal).ToArray();
                if (values.Length > 0)
                {
                    _ = b.RequireClaim(group.Key, values);
                }
            }
        }

        _policies[cfg.Name] = b.Build();
    }

    /// <summary>
    /// Sets the default and fallback authorization policies.
    /// </summary>
    /// <param name="requireAuthenticatedFallback">Whether to require authentication for the fallback policy.</param>
    /// <param name="defaultRoles">The default roles to require for the default policy.</param>
    public static void SetDefaults(bool requireAuthenticatedFallback, string[]? defaultRoles)
    {
        _fallbackPolicy = requireAuthenticatedFallback
            ? new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
            : null;

        _defaultPolicy = (defaultRoles is { Length: > 0 })
            ? new AuthorizationPolicyBuilder().RequireRole(defaultRoles).Build()
            : null;
    }

    internal static bool TryGet(string name, out AuthorizationPolicy policy) => _policies.TryGetValue(name, out policy!);
    internal static AuthorizationPolicy? DefaultPolicy => _defaultPolicy;
    internal static AuthorizationPolicy? FallbackPolicy => _fallbackPolicy;
}
