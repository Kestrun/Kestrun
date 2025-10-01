using Kestrun.Languages;
using Kestrun.SharedState;
using Microsoft.CodeAnalysis;
using SerilogLogger = Serilog.ILogger;
using Serilog.Events;

namespace Kestrun.Health;
/// <summary>
/// A health probe implemented via a VB.NET script.
/// </summary>
/// <param name="name">The name of the probe.</param>
/// <param name="tags">The tags associated with the probe.</param>
/// <param name="runner">The script runner to execute the probe.</param>
/// <param name="locals">The local variables for the script.</param>
/// <param name="logger">The logger to use for logging.</param>
internal sealed class VbScriptProbe(
    string name,
    IEnumerable<string>? tags,
    Func<CsGlobals, Task<ProbeResult>> runner,
    IReadOnlyDictionary<string, object?>? locals,
    SerilogLogger logger) : IProbe
{
    /// <summary>
    /// The script runner to execute the probe.
    /// </summary>
    private readonly Func<CsGlobals, Task<ProbeResult>> _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    private readonly IReadOnlyDictionary<string, object?>? _locals = locals;
    /// <summary>
    /// The logger to use for logging.
    /// </summary>
    private readonly SerilogLogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    /// <summary>
    /// Gets the name of the probe.
    /// </summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Probe name cannot be null or empty.", nameof(name))
        : name;

    /// <summary>
    /// Gets the tags associated with the probe.
    /// </summary>
    public string[] Tags { get; } = tags is null
        ? []
        : [.. tags.Where(static t => !string.IsNullOrWhiteSpace(t))
                      .Select(static t => t.Trim())
                      .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <inheritdoc />
    public SerilogLogger Logger { get; init; } = logger;

    /// <summary>
    /// Executes the VB.NET script and returns the resulting ProbeResult.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a ProbeResult as the result.</returns>
    public async Task<ProbeResult> CheckAsync(CancellationToken ct = default)
    {
        var globals = _locals is { Count: > 0 }
            ? new CsGlobals(SharedStateStore.Snapshot(), _locals)
            : new CsGlobals(SharedStateStore.Snapshot());
        try
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("VbScriptProbe {Probe} executing", Name);
            }
            var result = await _runner(globals).WaitAsync(ct).ConfigureAwait(false)
                ?? new ProbeResult(ProbeStatus.Unhealthy, "Script returned null result");
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("VbScriptProbe {Probe} completed status={Status}", Name, result.Status);
            }
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "VB.NET health probe {Probe} failed.", Name);
            return new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}");
        }
    }
}
