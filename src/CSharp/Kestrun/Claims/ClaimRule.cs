namespace Kestrun.Claims;

/// <summary>Represents one claim must equal rule.</summary>
/// <remarks>
/// This is used to define authorization policies that require a specific claim type
/// with specific allowed values.
/// It is typically used in conjunction with <see cref="ClaimPolicyConfig"/> to define
/// multiple policies.
/// </remarks>
public sealed record ClaimRule
{
    /// <summary>
    /// The claim type required by this rule.
    /// </summary>
    public string ClaimType { get; }

    /// <summary>
    /// Description of the claim rule.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Allowed values for the claim. Exposed as a read-only sequence.
    /// </summary>
    public IReadOnlyList<string> AllowedValues { get; }

    /// <summary>
    /// Constructs a rule from a claim type and one or more allowed values.
    /// </summary>
    /// <param name="claimType">The claim type required by this rule.</param>
    /// <param name="description">Description of the claim rule.</param>
    /// <param name="allowedValues">Allowed values for the claim.</param>
    public ClaimRule(string claimType, string? description, params string[] allowedValues)
    {
        ClaimType = claimType ?? throw new ArgumentNullException(nameof(claimType));
        Description = description;
        // Make a defensive copy to avoid exposing caller-owned mutable arrays.
        AllowedValues = (allowedValues is null) ? Array.Empty<string>() : Array.AsReadOnly((string[])allowedValues.Clone());
    }

    /// <summary>
    /// Constructs a rule from a claim type and an explicit read-only list of values.
    /// </summary>
    /// <param name="claimType">The claim type required by this rule.</param>
    /// <param name="description">Description of the claim rule.</param>
    /// <param name="allowedValues">Allowed values for the claim.</param>
    public ClaimRule(string claimType, string? description, IReadOnlyList<string> allowedValues)
    {
        ClaimType = claimType ?? throw new ArgumentNullException(nameof(claimType));
        Description = description;
        AllowedValues = allowedValues ?? [];
    }
    /// <summary>
    /// Constructs a rule from a claim type and one or more allowed values without description.
    /// </summary>
    /// <param name="claimType">The claim type required by this rule.</param>
    /// <param name="allowedValues">Allowed values for the claim.</param>
    public ClaimRule(string claimType, IReadOnlyList<string> allowedValues)
        : this(claimType, null, allowedValues) { }
}
