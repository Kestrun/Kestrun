using Kestrun.Languages;
using Kestrun.SharedState;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using RoslynCompilationErrorException = Microsoft.CodeAnalysis.Scripting.CompilationErrorException;
using SerilogLogger = Serilog.ILogger;
using Serilog.Events;

namespace Kestrun.Health;
/// <summary>
/// A health probe implemented via a C# script.
/// </summary>
/// <param name="name">The name of the probe.</param>
/// <param name="tags">The tags associated with the probe.</param>
/// <param name="runner">The script runner to execute the probe.</param>
/// <param name="locals">The local variables for the script.</param>
/// <param name="logger">The logger to use for logging.</param>
internal sealed class CSharpScriptProbe(
    string name,
    IEnumerable<string>? tags,
    ScriptRunner<ProbeResult> runner,
    IReadOnlyDictionary<string, object?>? locals,
    SerilogLogger logger) : IProbe
{
    /// <summary>
    /// The script runner to execute the probe.
    /// </summary>
    private readonly ScriptRunner<ProbeResult> _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    /// <summary>
    /// The local variables for the script.
    /// </summary>
    private readonly IReadOnlyDictionary<string, object?>? _locals = locals;
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
    public SerilogLogger Logger { get; init; } = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Executes the C# script and returns the resulting ProbeResult.
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
