using Microsoft.AspNetCore.Authorization;
using Kestrun.Authorization;


namespace Kestrun.Hosting;

/// <summary>
/// Provides extension methods for adding authorization schemes to the Kestrun host.
/// </summary>
public static class KestrunHostAuthorizationExtensions
{

    /// <summary>
    /// Adds authorization services to the Kestrun host.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="cfg">Optional configuration for authorization options.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost AddAuthorization(this KestrunHost host, Action<AuthorizationOptions>? cfg = null)
    {
        return host.AddService(services =>
        {
            //_ = cfg == null ? services.AddAuthorization() : services.AddAuthorization(cfg);
            _ = services.AddAuthorization(o => cfg?.Invoke(o)); // optional extra code config
            _ = services.AddSingleton<IAuthorizationPolicyProvider, KestrunDynamicPolicyProvider>();
        });
    }

    /// <summary>
    /// Adds authorization services to the Kestrun host using the provided configuration.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="config">The authorization configuration.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the configuration is invalid.</exception>
    public static KestrunHost AddAuthorization(this KestrunHost host, KestrunPolicyConfig config)
    {
        KestrunAuthorizationRegistry.AddOrUpdatePolicy(config);
        return host;
    }

    /// <summary>
    /// Sets the default authorization settings for the Kestrun host.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="requireAuthenticatedFallback">Indicates whether authenticated fallback is required.</param>
    /// <param name="defaultRoles">The default roles to assign.</param>
    /// <returns>The configured KestrunHost instance.</returns>
    public static KestrunHost SetAuthorizationDefaults(this KestrunHost host, bool requireAuthenticatedFallback, string[]? defaultRoles = null)
    {
        KestrunAuthorizationRegistry.SetDefaults(requireAuthenticatedFallback, defaultRoles);
        return host;
    }

    /// <summary>
    /// Checks if the specified authorization policy is registered in the Kestrun host.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="policyName">The name of the authorization policy to check.</param>
    /// <returns>True if the policy is registered; otherwise, false.</returns>
    public static bool HasAuthzPolicy(this KestrunHost host, string policyName)
    {
        var policyProvider = host.App.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = policyProvider.GetPolicyAsync(policyName).GetAwaiter().GetResult();
        return policy != null;
    }
}
