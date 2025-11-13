using Kestrun.Languages;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using RoslynCompilationErrorException = Microsoft.CodeAnalysis.Scripting.CompilationErrorException;
using SerilogLogger = Serilog.ILogger;
using Serilog.Events;
using Kestrun.Hosting;

namespace Kestrun.Health;
/// <summary>
/// A health probe implemented via a C# script.
/// </summary>
/// <param name="host">The Kestrun host instance.</param>
/// <param name="name">The name of the probe.</param>
/// <param name="tags">The tags associated with the probe.</param>
/// <param name="runner">The script runner to execute the probe.</param>
/// <param name="locals">The local variables for the script.</param>
internal sealed class CSharpScriptProbe(
    KestrunHost host,
    string name,
    IEnumerable<string>? tags,
    ScriptRunner<ProbeResult> runner,
    IReadOnlyDictionary<string, object?>? locals) : Probe(name, tags), IProbe
{
    private SerilogLogger Logger => host.Logger;
    /// <summary>
    /// The script runner to execute the probe.
    /// </summary>
    private readonly ScriptRunner<ProbeResult> _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    /// <summary>
    /// The local variables for the script.
    /// </summary>
    private readonly IReadOnlyDictionary<string, object?>? _locals = locals;

    /// <inheritdoc/>
    public override async Task<ProbeResult> CheckAsync(CancellationToken ct = default)
    {
        var globals = _locals is { Count: > 0 }
            ? new CsGlobals(host.SharedState.Snapshot(), _locals)
            : new CsGlobals(host.SharedState.Snapshot());
        try
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("CSharpScriptProbe {Probe} executing", Name);
            }
            var result = await _runner(globals, ct).ConfigureAwait(false)
                ?? new ProbeResult(ProbeStatus.Unhealthy, "Script returned null result");
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("CSharpScriptProbe {Probe} completed status={Status}", Name, result.Status);
            }
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (RoslynCompilationErrorException ex)
        {
            Logger.Error(ex, "C# health probe {Probe} failed to execute.", Name);
            return new ProbeResult(ProbeStatus.Unhealthy, string.Join("; ", ex.Diagnostics.Select(static d => d.GetMessage())));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "C# health probe {Probe} threw an exception.", Name);
            return new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}");
        }
    }
}
