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
    /// <param name="description">Description of the claim rule.</param>
    /// <param name="allowedValues">Allowed values for the claim.</param>
    /// <returns>The current builder instance.</returns>
    public ClaimPolicyBuilder AddPolicy(string policyName, string claimType, string description, params string[] allowedValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);
        if (allowedValues is null || allowedValues.Length == 0)
        {
            throw new ArgumentException("At least one allowed value must be specified.", nameof(allowedValues));
        }

        _policies[policyName] = new ClaimRule(claimType, description, allowedValues);
        return this;
    }

    /// <summary>
    /// Adds a new policy with a required claim rule using a <see cref="UserIdentityClaim"/>.
    /// </summary>
    /// <param name="policyName">The name of the policy.</param>
    /// <param name="claimType">The required <see cref="UserIdentityClaim"/> type.</param>
    /// <param name="description">Description of the claim rule.</param>
    /// <param name="allowedValues">Allowed values for the claim.</param>
    /// <returns>The current builder instance.</returns>
    public ClaimPolicyBuilder AddPolicy(string policyName, UserIdentityClaim claimType, string? description, params string[] allowedValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        if (allowedValues is null || allowedValues.Length == 0)
        {
            throw new ArgumentException("At least one allowed value must be specified.", nameof(allowedValues));
        }

        _policies[policyName] = new ClaimRule(claimType.ToClaimUri(), description, allowedValues);
        return this;
    }
    /// <summary>
    /// Adds a prebuilt claim rule under a policy name.
    /// </summary>
    /// <param name="policyName">The name of the policy.</param>
    /// <param name="rule">The claim rule to associate with the policy.</param>
    /// <param name="description">Description of the claim rule.</param>
    /// <returns>The current builder instance.</returns>
    public ClaimPolicyBuilder AddPolicy(string policyName, ClaimRule rule, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(rule);
        if (description is not null)
        {
            rule.Description = description;
        }
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
    /// <summary>
    /// Returns a string representation of the builder.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => $"ClaimPolicyBuilder: {_policies.Count} policies defined.";
    /// <summary>
    /// Clears all defined policies from the builder.
    /// </summary>
    public void Clear() => _policies.Clear();

    /// <summary>
    /// Creates a new instance of the <see cref="ClaimPolicyBuilder"/>.
    /// </summary>
    /// <returns>A new instance of <see cref="ClaimPolicyBuilder"/>.</returns>
    public static ClaimPolicyBuilder Create() => new();
}
