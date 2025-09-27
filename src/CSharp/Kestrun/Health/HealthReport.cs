namespace Kestrun.Health;

/// <summary>
/// Represents an aggregated health report produced by <see cref="HealthProbeRunner"/>.
/// </summary>
/// <param name="Status">Overall status calculated from all probe results.</param>
/// <param name="StatusText">Lowercase textual representation of <see cref="Status"/>.</param>
/// <param name="GeneratedAt">UTC timestamp the report was produced.</param>
/// <param name="Probes">Detailed results for each executed probe.</param>
/// <param name="Summary">Aggregated counts per <see cref="ProbeStatus"/>.</param>
/// <param name="AppliedTags">Tag filters applied to this report.</param>
public sealed record HealthReport(
    ProbeStatus Status,
    string StatusText,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<HealthProbeEntry> Probes,
    HealthSummary Summary,
    IReadOnlyList<string> AppliedTags);
