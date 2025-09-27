namespace Kestrun.Health;

/// <summary>
/// Health probe status enumeration.
/// </summary>
public enum ProbeStatus
{
    /// <summary>
    /// The probe is healthy.
    /// </summary>
    Healthy = 0,
    /// <summary>
    /// The probe is degraded.
    /// </summary>
    Degraded = 1,
    /// <summary>
    /// The probe is unhealthy.
    /// </summary>
    Unhealthy = 2
}

