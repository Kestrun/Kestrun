using System.Management.Automation;
using System.Reflection;
using Kestrun.Scripting;
using Kestrun.Utilities;
using Microsoft.CodeAnalysis.CSharp;
using Serilog.Events;

namespace Kestrun.Scheduling;

internal static class JobFactory
{
    internal record JobConfig(
          ScriptLanguage Language,
          string Code,
          Serilog.ILogger Log,
          KestrunRunspacePoolManager? Pool = null,
          string[]? ExtraImports = null,
          Assembly[]? ExtraRefs = null,
            IReadOnlyDictionary<string, object?>? Locals = null,
          LanguageVersion LanguageVersion = LanguageVersion.CSharp12
          );

    internal static Func<CancellationToken, Task> Create(JobConfig config)
    {
        return config.Language switch
        {
            ScriptLanguage.PowerShell =>
                config.Pool is null
                    ? throw new InvalidOperationException("PowerShell runspace pool must be provided for PowerShell jobs.")
                    : PowerShellJob(config),
            ScriptLanguage.CSharp => RoslynJob(config),
            ScriptLanguage.VBNet => RoslynJob(config),
            _ => throw new NotSupportedException($"Language {config.Language} not supported.")
        };
    }

    public static async Task<Func<CancellationToken, Task>> CreateAsync(
     JobConfig config, FileInfo fileInfo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(fileInfo.FullName);
        }

        var updatedConfig = config with { Code = await File.ReadAllTextAsync(fileInfo.FullName, ct) };
        if (updatedConfig.Log.IsEnabled(LogEventLevel.Debug))
        {
            updatedConfig.Log.Debug("Creating job for {File} with language {Lang}", fileInfo.FullName, updatedConfig.Language);
        }

        return Create(updatedConfig);
    }

    public static Func<CancellationToken, Task> Create(
        JobConfig config, FileInfo fileInfo) => CreateAsync(config, fileInfo).GetAwaiter().GetResult();


    /* ----------------  PowerShell  ---------------- */
    private static Func<CancellationToken, Task> PowerShellJob(
       JobConfig config)
    {
        return async ct =>
        {
            if (config.Log.IsEnabled(LogEventLevel.Debug))
            {
                config.Log.Debug("Building PowerShell delegate, script length={Length}", config.Code?.Length);
            }

            if (config.Pool is null)
            {
                throw new InvalidOperationException("PowerShell runspace pool must be provided for PowerShell jobs.");
            }

            var runspace = config.Pool.Acquire();
            try
            {
                using var ps = PowerShell.Create();
                ps.Runspace = runspace;
                _ = ps.AddScript(config.Code);
                if (config.Log.IsEnabled(LogEventLevel.Debug))
                {
                    config.Log.Debug("Executing PowerShell script with {RunspaceId} - {Preview}", runspace.Id, config.Code?[..Math.Min(40, config.Code.Length)]);
                }

                // Register cancellation
                using var reg = ct.Register(() => ps.Stop());

                // Wait for the PowerShell script to complete
                var psResults = await ps.InvokeAsync().WaitAsync(ct).ConfigureAwait(false);

                config.Log.Verbose($"PowerShell script executed with {psResults.Count} results.");
                if (config.Log.IsEnabled(LogEventLevel.Debug))
                {
                    config.Log.Debug("PowerShell script output:");
                    foreach (var r in psResults.Take(10))      // first 10 only
                    {
                        config.Log.Debug("   • {Result}", r);
                    }

                    if (psResults.Count > 10)
                    {
                        config.Log.Debug("   … {Count} more", psResults.Count - 10);
                    }
                }

                if (ps.HadErrors || ps.Streams.Error.Count != 0 || ps.Streams.Verbose.Count > 0 || ps.Streams.Debug.Count > 0 || ps.Streams.Warning.Count > 0 || ps.Streams.Information.Count > 0)
                {
                    config.Log.Verbose("PowerShell script completed with verbose/debug/warning/info messages.");
                    config.Log.Verbose(BuildError.Text(ps));
                }
            }
            catch (Exception ex)
            {
                config.Log.Error(ex, "PowerShell job failed - {Preview}", config.Code?[..Math.Min(40, config.Code.Length)]);
                throw;
            }
            finally
            {
                if (config.Log.IsEnabled(LogEventLevel.Debug))
                {
                    config.Log.Debug("PowerShell job completed, releasing runspace back to pool.");
                }
                // Ensure we release the runspace back to the pool
                config.Pool.Release(runspace);
            }
        };
    }

    /// <summary>
    /// Creates a C# or VB.NET job using Roslyn compilation.
    /// </summary>
    /// <param name="config">The job configuration containing code, logger, and other parameters.</param>
    /// <returns>A function that executes the job.</returns>
    /// <remarks>
    /// This method uses Roslyn to compile and execute C# or VB.NET code.
    /// </remarks>
    private static Func<CancellationToken, Task> RoslynJob(JobConfig config) => RoslynJobFactory.Build(config.Code, config.Log, config.ExtraImports, config.ExtraRefs, config.Locals, config.LanguageVersion);
}
