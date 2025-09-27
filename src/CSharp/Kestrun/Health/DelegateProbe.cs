namespace Kestrun.Health;

/// <summary>
/// Simple <see cref="IProbe"/> implementation that delegates execution to a user-supplied asynchronous function.
/// </summary>
internal sealed class DelegateProbe(string name, IEnumerable<string>? tags, Func<CancellationToken, Task<ProbeResult>> callback, Serilog.ILogger? logger = null) : IProbe
{
    private readonly Func<CancellationToken, Task<ProbeResult>> _callback = callback ?? throw new ArgumentNullException(nameof(callback));

    /// <inheritdoc />
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Probe name cannot be null or empty.", nameof(name))
        : name;

    /// <inheritdoc />
    public string[] Tags { get; } = tags?.Where(static t => !string.IsNullOrWhiteSpace(t))
                                       .Select(static t => t.Trim())
                                       .Distinct(StringComparer.OrdinalIgnoreCase)
                                       .ToArray() ?? [];

    /// <inheritdoc />
    public Serilog.ILogger Logger { get; init; } = logger ?? Serilog.Log.ForContext("HealthProbe", name).ForContext("Probe", name);

    /// <inheritdoc />
    public Task<ProbeResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            return _callback(ct);
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}"));
        }
    }
}
