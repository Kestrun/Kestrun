namespace Kestrun.Health;

/// <summary>
/// Provides string constants for well-known probe statuses that dynamic scripts may return.
/// These values are used when converting script outputs into ProbeResult statuses.
/// </summary>
public static class ProbeStatusLabels
{
    /// <summary>
    /// Represents the "ok" status.
    /// </summary>
    public const string STATUS_OK = "ok";

    /// <summary>
    /// Represents the "healthy" status.
    /// </summary>
    public const string STATUS_HEALTHY = "healthy";

    /// <summary>
    /// Represents the short "warn" status.
    /// </summary>
    public const string STATUS_WARN = "warn";

    /// <summary>
    /// Represents the "warning" status.
    /// </summary>
    public const string STATUS_WARNING = "warning";

    /// <summary>
    /// Represents the "degraded" status.
    /// </summary>
    public const string STATUS_DEGRADED = "degraded";

    /// <summary>
    /// Represents the short "fail" status.
    /// </summary>
    public const string STATUS_FAIL = "fail";

    /// <summary>
    /// Represents the "failed" status.
    /// </summary>
    public const string STATUS_FAILED = "failed";

    /// <summary>
    /// Represents the "unhealthy" status.
    /// </summary>
    public const string STATUS_UNHEALTHY = "unhealthy";
}
