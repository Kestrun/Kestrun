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
    /// <summary>The claim type required by this rule.</summary>
    public string ClaimType { get; }

    /// <summary>Allowed values for the claim. Exposed as a read-only sequence.</summary>
    public IReadOnlyList<string> AllowedValues { get; }

    /// <summary>Constructs a rule from a claim type and one or more allowed values.</summary>
    public ClaimRule(string claimType, params string[] allowedValues)
    {
        ClaimType = claimType ?? throw new ArgumentNullException(nameof(claimType));
        // Make a defensive copy to avoid exposing caller-owned mutable arrays.
        AllowedValues = (allowedValues is null) ? Array.Empty<string>() : Array.AsReadOnly((string[])allowedValues.Clone());
    }

    /// <summary>Constructs a rule from a claim type and an explicit read-only list of values.</summary>
    public ClaimRule(string claimType, IReadOnlyList<string> allowedValues)
    {
        ClaimType = claimType ?? throw new ArgumentNullException(nameof(claimType));
        AllowedValues = allowedValues ?? [];
    }
}
