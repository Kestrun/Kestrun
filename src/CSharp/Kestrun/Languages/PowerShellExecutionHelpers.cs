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
        var proxy = ps.Runspace!.SessionStateProxy;
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
    internal static async Task<PSDataCollection<PSObject>> InvokeAsync(PowerShell ps, Serilog.ILogger log, CancellationToken ct)
    {
        if (log.IsEnabled(LogEventLevel.Verbose))
        {
            log.Verbose("Executing PowerShell script...");
        }

        using var registration = ct.Register(static state =>
        {
            var shell = (PowerShell)state!;
            try { shell.Stop(); } catch { /* ignored */ }
        }, ps);

        var results = await ps.InvokeAsync().ConfigureAwait(false);

        if (log.IsEnabled(LogEventLevel.Verbose))
        {
            log.Verbose("PowerShell script executed with {Count} results.", results.Count);
        }
        return results;
    }
}
