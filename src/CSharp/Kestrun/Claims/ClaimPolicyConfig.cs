namespace Kestrun.Claims;


/// <summary>A bag of named policies, each backed by a ClaimRule.</summary>
/// <remarks>
/// This is used to define multiple authorization policies in a structured way.
/// </remarks>
public sealed class ClaimPolicyConfig
{
    /// <summary>
    /// Gets the dictionary of named policies, each backed by a <see cref="ClaimRule"/>.
    /// </summary>
    public Dictionary<string, ClaimRule> Policies { get; init; } = [];

    /// <summary>
    /// Gets the number of defined policies.
    /// </summary>
    public int Count => Policies.Count;

    /// <summary>
    /// Gets the names of all defined policies.
    /// </summary>
    public IEnumerable<string> PolicyNames => Policies.Keys;

    /// <summary>
    /// Gets a policy by name.
    /// </summary>
    /// <param name="policyName">The name of the policy to retrieve.</param>
    /// <returns>The <see cref="ClaimRule"/> associated with the specified policy name, or null if not found.</returns>
    public ClaimRule? GetPolicy(string policyName) => Policies.TryGetValue(policyName, out var rule) ? rule : null;

    /// <summary>
    /// Add a policy by name.
    /// </summary>
    /// <param name="policyName">The name of the policy to set.</param>
    /// <param name="rule">The <see cref="ClaimRule"/> to associate with the specified policy name.</param>
    public void AddPolicy(string policyName, ClaimRule rule) => Policies[policyName] = rule;

    /// <summary>
    /// Add multiple policies.
    /// </summary>
    /// <param name="policies">The collection of policies to add.</param>
    public void AddPolicies(IEnumerable<KeyValuePair<string, ClaimRule>> policies)
    {
        foreach (var kvp in policies)
        {
            Policies[kvp.Key] = kvp.Value;
        }
    }
}
