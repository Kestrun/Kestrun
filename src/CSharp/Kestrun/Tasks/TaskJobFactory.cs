using System.Management.Automation;
using Kestrun.Scripting;
using Kestrun.SharedState;
using Kestrun.Utilities;
using Kestrun.Languages;
using Kestrun.Hosting.Options;

namespace Kestrun.Tasks;

/// <summary>
/// Factory to create task job delegates for different scripting languages.
/// </summary>
internal static class TaskJobFactory
{
    /// <summary>
    /// Configuration for creating a task job delegate.
    /// </summary>
    /// <param name="ScriptCode">The language options containing the script code and settings.</param>
    /// <param name="Log">Logger instance for logging.</param>
    /// <param name="Pool">Optional PowerShell runspace pool.</param>
    /// <param name="Progress">Progress state object to expose to scripts.</param>
    internal record TaskJobConfig(
        LanguageOptions ScriptCode,
        Serilog.ILogger Log,
        KestrunRunspacePoolManager? Pool,
        ProgressiveKestrunTaskState Progress
    );

    internal static Func<CancellationToken, Task<object?>> Create(TaskJobConfig config)
        => config.ScriptCode.Language switch
        {
            ScriptLanguage.PowerShell =>
                config.Pool is null
                    ? throw new InvalidOperationException("PowerShell runspace pool must be provided for PowerShell tasks.")
                    : PowerShellTask(config),
            ScriptLanguage.CSharp => CSharpTask(config),
            ScriptLanguage.VBNet => VbNetTask(config),
            _ => throw new NotSupportedException($"Language {config.ScriptCode.Language} not supported."),
        };

    private static Func<CancellationToken, Task<object?>> PowerShellTask(TaskJobConfig config)
    {
        return async ct =>
        {
            if (config.Pool is null)
            {
                throw new InvalidOperationException("PowerShell runspace pool must be provided for PowerShell tasks.");
            }
            var runspace = config.Pool.Acquire();
            try
            {
                using var ps = PowerShell.Create();
                ps.Runspace = runspace;
                _ = ps.AddScript(config.ScriptCode.Code);

                // Merge arguments and inject Progress variable for cooperative updates
                var vars = config.ScriptCode.Arguments is { Count: > 0 }
                    ? new Dictionary<string, object?>(config.ScriptCode.Arguments)
                    : [];
                vars["TaskProgress"] = config.Progress;
                PowerShellExecutionHelpers.SetVariables(ps, vars, config.Log);
                using var reg = ct.Register(() => ps.Stop());
                var results = await ps.InvokeAsync().WaitAsync(ct).ConfigureAwait(false);

                // Collect pipeline output (base objects) as an object[]
                var output = results.Count == 0
                    ? null
                    : results.Select(r => r.BaseObject).ToArray();

                if (ps.HadErrors || ps.Streams.Error.Count != 0 ||
                    ps.Streams.Warning.Count > 0 || ps.Streams.Verbose.Count > 0 ||
                    ps.Streams.Debug.Count > 0 || ps.Streams.Information.Count > 0)
                {
                    config.Log.Verbose(BuildError.Text(ps));
                }

                return output;
            }
            finally
            {
                config.Pool.Release(runspace);
            }
        };
    }

    private static Func<CancellationToken, Task<object?>> CSharpTask(TaskJobConfig config)
    {
        // Prepare locals upfront and include TaskProgress so compilation preamble declares it
        var compileLocals = config.ScriptCode.Arguments is { Count: > 0 }
            ? new Dictionary<string, object?>(config.ScriptCode.Arguments)
            : [];
        compileLocals["TaskProgress"] = config.Progress;

        // Compile and get a runner that returns object? (last expression)
        var script = CSharpDelegateBuilder.Compile(
            config.ScriptCode.Code,
            config.Log,
            config.ScriptCode.ExtraImports,
            config.ScriptCode.ExtraRefs,
            compileLocals,
            config.ScriptCode.LanguageVersion);
        var runner = script.CreateDelegate(); // ScriptRunner<object?>
        return async ct =>
        {
            // Use the same locals at execution time
            var globals = new CsGlobals(SharedStateStore.Snapshot(), compileLocals);
            return await runner(globals, ct).ConfigureAwait(false);
        };
    }

    private static Func<CancellationToken, Task<object?>> VbNetTask(TaskJobConfig config)
    {
        var runner = VBNetDelegateBuilder.Compile<object>(config.ScriptCode.Code, config.Log, config.ScriptCode.ExtraImports, config.ScriptCode.ExtraRefs, config.ScriptCode.Arguments, Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16_9);
        return async ct =>
        {
            // For VB, expose Progress via locals as well
            var locals = config.ScriptCode.Arguments is { Count: > 0 }
                ? new Dictionary<string, object?>(config.ScriptCode.Arguments)
                : [];
            locals["TaskProgress"] = config.Progress;
            var globals = new CsGlobals(SharedStateStore.Snapshot(), locals);
            // VB compiled delegate does not accept CancellationToken; allow cooperative cancel of awaiting.
            var task = runner(globals);
            var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, ct)).ConfigureAwait(false);
            if (completed == task)
            {
                return await task.ConfigureAwait(false);
            }
            ct.ThrowIfCancellationRequested();
            return null; // unreachable
        };
    }
}
