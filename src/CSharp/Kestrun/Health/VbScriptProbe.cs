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
    SerilogLogger logger) : Probe(name, tags, logger), IProbe
{
    /// <summary>
    /// The script runner to execute the probe.
    /// </summary>
    private readonly Func<CsGlobals, Task<ProbeResult>> _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    private readonly IReadOnlyDictionary<string, object?>? _locals = locals;

    /// <inheritdoc/>
    public override async Task<ProbeResult> CheckAsync(CancellationToken ct = default)
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
            Logger.Error(ex, "VB.NET health probe {Probe} failed.", Name);
            return new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}");
        }
    }
}
