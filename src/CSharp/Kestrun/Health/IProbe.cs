namespace Kestrun.Health;

/// <summary>
/// Defines a health probe that can be checked asynchronously.
/// </summary>
public interface IProbe
{
    /// <summary>
    /// Tag indicating the probe is a self-check of the health system itself.
    /// </summary>
    const string TAG_SELF = "self";   // internal use only, not for user-defined probes

    /// <summary>
    /// The name of the probe.
    /// </summary>
    string Name { get; }        // e.g., "disk", "sql", "extApi"

    /// <summary>
    /// The tags associated with the probe.
    /// </summary>
    /// <remarks>
    /// Tags can be used to group probes or indicate their purpose.
    /// </remarks>
    string[] Tags { get; }      // e.g., ["live"], ["ready"], both, or custom

    /// <summary>
    /// Checks the health of the probe asynchronously.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a <see cref="ProbeResult"/> as the result.</returns>
    Task<ProbeResult> CheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Logger used for diagnostics within the probe.
    /// </summary>
    Serilog.ILogger Logger { get; init; }
}
