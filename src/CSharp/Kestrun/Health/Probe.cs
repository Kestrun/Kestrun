namespace Kestrun.Health;

/// <summary>
/// Base class for health probes.
/// </summary>
/// <param name="name">The name of the probe.</param>
/// <param name="tags">The tags associated with the probe.</param>
/// <param name="logger">The logger instance.</param>
public abstract class Probe(string name, IEnumerable<string>? tags, Serilog.ILogger logger) : IProbe
{
    /// <inheritdoc/>
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
          ? throw new ArgumentException("Probe name cannot be null or empty.", nameof(name))
          : name;

    /// <inheritdoc/>
    public string[] Tags { get; } = tags is null
        ? []
        : [.. tags.Where(static t => !string.IsNullOrWhiteSpace(t))
                      .Select(static t => t.Trim())
                      .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <inheritdoc/>
    public Serilog.ILogger Logger { get; init; } = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public abstract Task<ProbeResult> CheckAsync(CancellationToken ct);
}
