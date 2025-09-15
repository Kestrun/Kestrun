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
}

