using System.Management.Automation;
using Kestrun.Scripting;
using Kestrun.Utilities;
using Kestrun.Languages;
using Kestrun.Hosting.Options;
using Kestrun.Hosting;

namespace Kestrun.Tasks;

/// <summary>
/// Factory to create task job delegates for different scripting languages.
/// </summary>
internal static class TaskJobFactory
{
    /// <summary>
    /// Configuration for creating a task job delegate.
    /// </summary>
    /// <param name="Host">The Kestrun host instance.</param>
    /// <param name="TaskId">Unique identifier of the task.</param>
    /// <param name="ScriptCode">The language options containing the script code and settings.</param>
    /// <param name="Pool">Optional PowerShell runspace pool.</param>
    /// <param name="Progress">Progress state object to expose to scripts.</param>
    internal record TaskJobConfig(
        KestrunHost Host,
        string TaskId,
        LanguageOptions ScriptCode,
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
            var log = config.Host.Logger;
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
                vars["TaskId"] = config.TaskId;
                PowerShellExecutionHelpers.SetVariables(ps, vars, log);
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
                    log.Verbose(BuildError.Text(ps));
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
            host: config.Host,
            code: config.ScriptCode.Code,
            extraImports: config.ScriptCode.ExtraImports,
            extraRefs: config.ScriptCode.ExtraRefs,
            locals: compileLocals,
            languageVersion: config.ScriptCode.LanguageVersion);
        var runner = script.CreateDelegate(); // ScriptRunner<object?>
        return async ct =>
        {
            // Use the same locals at execution time
            var globals = new CsGlobals(config.Host.SharedState.Snapshot(), compileLocals);
            return await runner(globals, ct).ConfigureAwait(false);
        };
    }

    private static Func<CancellationToken, Task<object?>> VbNetTask(TaskJobConfig config)
    {
        var code = config.ScriptCode.Code;
        var log = config.Host.Logger;
        var arguments = config.ScriptCode.Arguments;
        var runner = VBNetDelegateBuilder.Compile<object>(
            host: config.Host,
            code: config.ScriptCode.Code,
            extraImports: config.ScriptCode.ExtraImports,
            extraRefs: config.ScriptCode.ExtraRefs,
            locals: arguments,
            languageVersion: Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16_9
        );
        return async ct =>
        {
            // For VB, expose Progress via locals as well
            var locals = arguments is { Count: > 0 }
                ? new Dictionary<string, object?>(arguments)
                : [];
            locals["TaskProgress"] = config.Progress;
            var globals = new CsGlobals(config.Host.SharedState.Snapshot(), locals);
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
