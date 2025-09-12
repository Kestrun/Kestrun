// Kestrun.Authorization.KestrunDynamicPolicyProvider.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Kestrun.Authorization;

/// <summary>
/// An authorization policy provider that retrieves policies from the KestrunAuthorizationRegistry.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KestrunDynamicPolicyProvider"/> class.
/// </remarks>
/// <param name="options">The authorization options.</param>
public sealed class KestrunDynamicPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _inner = new(options);

    /// <summary>
    /// Gets a policy by name.
    /// </summary>
    /// <param name="policyName">The name of the policy.</param>
    /// <returns></returns>
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        => KestrunAuthorizationRegistry.TryGet(policyName, out var p)
           ? Task.FromResult<AuthorizationPolicy?>(p)
           : _inner.GetPolicyAsync(policyName);

    /// <summary>
    /// Gets the default policy.
    /// </summary>
    /// <returns>The default authorization policy.</returns>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => KestrunAuthorizationRegistry.DefaultPolicy is { } p
           ? Task.FromResult(p)
           : _inner.GetDefaultPolicyAsync();

    /// <summary>
    /// Gets the fallback policy.
    /// </summary>
    /// <returns>The fallback authorization policy.</returns>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => KestrunAuthorizationRegistry.FallbackPolicy is { } p
           ? Task.FromResult<AuthorizationPolicy?>(p)
           : _inner.GetFallbackPolicyAsync();
}
