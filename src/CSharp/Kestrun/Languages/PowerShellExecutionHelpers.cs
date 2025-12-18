using System.Management.Automation;
using Serilog.Events;

namespace Kestrun.Languages;

/// <summary>
/// Shared helper methods for configuring and invoking PowerShell scripts in a consistent manner.
/// Extracted to reduce duplication between <see cref="PowerShellDelegateBuilder"/> and script-based probes.
/// </summary>
internal static class PowerShellExecutionHelpers
{
    /// <summary>
    /// Sets variables in the PowerShell runspace from the provided argument dictionary.
    /// Existing variables are overwritten. Null or empty collections are ignored.
    /// </summary>
    internal static void SetVariables(PowerShell ps, IReadOnlyDictionary<string, object?>? arguments, Serilog.ILogger log)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return;
        }

        if (log.IsEnabled(LogEventLevel.Verbose))
        {
            log.Verbose("Setting PowerShell variables from arguments: {Count}", arguments.Count);
        }
        var proxy = ps.Runspace.SessionStateProxy;
        foreach (var kv in arguments)
        {
            proxy.SetVariable(kv.Key, kv.Value);
        }
    }

    /// <summary>
    /// Adds a script block to the pipeline.
    /// </summary>
    internal static void AddScript(PowerShell ps, string code) => _ = ps.AddScript(code);

    /// <summary>
    /// Invokes the configured PowerShell pipeline asynchronously with cooperative cancellation.
    /// Cancellation attempts to stop the pipeline gracefully.
    /// </summary>
    internal static async Task<PSDataCollection<PSObject>> InvokeAsync(this PowerShell ps, Serilog.ILogger log, CancellationToken ct)
    {
        if (log.IsEnabled(LogEventLevel.Verbose))
        {
            log.Verbose("Executing PowerShell script...");
        }

        // If cancellation is already requested before the pipeline starts, do not invoke at all.
        // On some platforms/runtimes, calling Stop() before invocation begins is a no-op and
        // the pipeline may still start and run indefinitely.
        ct.ThrowIfCancellationRequested();

        using var registration = ct.Register(static state =>
        {
            var shell = (PowerShell)state!;
            try { shell.Stop(); } catch { /* ignored */ }
        }, ps);

        try
        {
            var invokeTask = ps.InvokeAsync();

            // If cancellation is requested during the short window between registration and invocation,
            // attempt to stop immediately.
            if (ct.IsCancellationRequested)
            {
                try { ps.Stop(); } catch { /* ignored */ }
            }

            var results = await invokeTask.ConfigureAwait(false);

            if (log.IsEnabled(LogEventLevel.Verbose))
            {
                log.Verbose("PowerShell script executed with {Count} results.", results.Count);
            }
            return results;
        }
        catch (PipelineStoppedException) when (ct.IsCancellationRequested)
        {
            // Convert PipelineStoppedException to OperationCanceledException when cancellation was requested
            throw new OperationCanceledException("PowerShell pipeline was stopped due to cancellation.", ct);
        }
    }
}
