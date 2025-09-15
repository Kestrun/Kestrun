namespace Kestrun.Claims;


/// <summary>
/// Builder for defining claim-based authorization policies.
/// </summary>
public sealed class ClaimPolicyBuilder
{
    private readonly Dictionary<string, ClaimRule> _policies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a new policy with a required claim rule.
    /// </summary>
    /// <param name="policyName">The name of the policy.</param>
    /// <param name="claimType">The required claim type.</param>
    /// <param name="allowedValues">Allowed values for the claim.</param>
    /// <returns>The current builder instance.</returns>
    public ClaimPolicyBuilder AddPolicy(string policyName, string claimType, params string[] allowedValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);
        if (allowedValues is null || allowedValues.Length == 0)
        {
            throw new ArgumentException("At least one allowed value must be specified.", nameof(allowedValues));
        }

        _policies[policyName] = new ClaimRule(claimType, allowedValues);
        return this;
    }

    /// <summary>
    /// Adds a new policy with a required claim rule using a <see cref="UserIdentityClaim"/>.
    /// </summary>
    /// <param name="policyName">The name of the policy.</param>
    /// <param name="claimType">The required <see cref="UserIdentityClaim"/> type.</param>
    /// <param name="allowedValues">Allowed values for the claim.</param>
    /// <returns>The current builder instance.</returns>
    public ClaimPolicyBuilder AddPolicy(string policyName, UserIdentityClaim claimType, params string[] allowedValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        if (allowedValues is null || allowedValues.Length == 0)
        {
            throw new ArgumentException("At least one allowed value must be specified.", nameof(allowedValues));
        }

        _policies[policyName] = new ClaimRule(claimType.ToClaimUri(), allowedValues);
        return this;
    }
    /// <summary>
    /// Adds a prebuilt claim rule under a policy name.
    /// </summary>
    public ClaimPolicyBuilder AddPolicy(string policyName, ClaimRule rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(rule);

        _policies[policyName] = rule;
        return this;
    }

    /// <summary>
    /// Gets the dictionary of all configured policies.
    /// </summary>
    public IReadOnlyDictionary<string, ClaimRule> Policies => _policies;

    /// <summary>
    /// Builds the configuration object.
    /// </summary>
    public ClaimPolicyConfig Build() => new()
    {
        Policies = new Dictionary<string, ClaimRule>(_policies, StringComparer.OrdinalIgnoreCase)
    };
}

