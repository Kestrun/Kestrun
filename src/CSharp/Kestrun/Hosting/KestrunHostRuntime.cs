using Kestrun.Forms;

namespace Kestrun.Hosting;
/// <summary>
/// Represents runtime information for a Kestrun host.
/// </summary>

public record KestrunHostRuntime
{
    /// <summary>
    /// Gets the form parsing options for Kestrun hosts.
    /// </summary>
    public Dictionary<string, KrFormOptions> FormOptions { get; } = [];


    /// <summary>
    /// Gets the timestamp when the Kestrun host was started.
    /// </summary>
    public DateTime? StartTime { get; internal set; }

    /// <summary>
    /// Gets the timestamp when the Kestrun host was stopped.
    /// </summary>
    public DateTime? StopTime { get; internal set; }

    /// <summary>
    /// Gets the uptime duration of the Kestrun host.
    /// While running (no StopTime yet), this returns DateTime.UtcNow - StartTime.
    /// After stopping, it returns StopTime - StartTime.
    /// If StartTime is not set, returns null.
    /// </summary>
    public TimeSpan? Uptime =>
        !StartTime.HasValue
            ? null
            : StopTime.HasValue
                ? StopTime - StartTime
                : DateTime.UtcNow - StartTime.Value;
}
