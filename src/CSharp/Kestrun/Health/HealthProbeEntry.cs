namespace Kestrun.Health;

/// <summary>
/// Represents the result of a single probe execution.
/// </summary>
/// <param name="Name">Probe name.</param>
/// <param name="Tags">Probe tags.</param>
/// <param name="Status">Probe status.</param>
/// <param name="StatusText">Lowercase textual status.</param>
/// <param name="Description">Optional description supplied by the probe.</param>
/// <param name="Data">Optional supplemental data.</param>
/// <param name="Duration">Probe execution duration.</param>
/// <param name="Error">Optional captured error details.</param>
public sealed record HealthProbeEntry(
    string Name,
    string[] Tags,
    ProbeStatus Status,
    string StatusText,
    string? Description,
    IReadOnlyDictionary<string, object>? Data,
    TimeSpan Duration,
    string? Error);
