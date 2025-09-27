namespace Kestrun.Health;

/// <summary>
/// Result of a health probe check.
/// </summary>
/// <param name="Status">The status of the probe.</param>
/// <param name="Description">A description of the probe result.</param>
/// <param name="Data">Additional data related to the probe result.</param>
public sealed record ProbeResult(
    ProbeStatus Status,
    string? Description = null,
    IReadOnlyDictionary<string, object>? Data = null
);
