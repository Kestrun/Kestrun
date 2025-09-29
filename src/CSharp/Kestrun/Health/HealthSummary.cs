namespace Kestrun.Health;

/// <summary>
/// Summary counts of probe results grouped by <see cref="ProbeStatus"/>.
/// </summary>
/// <param name="Total">Total number of executed probes.</param>
/// <param name="Healthy">Number of probes reporting <see cref="ProbeStatus.Healthy"/>.</param>
/// <param name="Degraded">Number of probes reporting <see cref="ProbeStatus.Degraded"/>.</param>
/// <param name="Unhealthy">Number of probes reporting <see cref="ProbeStatus.Unhealthy"/>.</param>
public sealed record HealthSummary(int Total, int Healthy, int Degraded, int Unhealthy);
