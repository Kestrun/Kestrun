using System;
using System.IO;
using System.Management.Automation;
using Kestrun.Scripting;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Kestrun.Razor;

/// <summary>
/// Provides middleware for enabling PowerShell-backed Razor Pages, allowing execution of a sibling PowerShell script (*.cshtml.ps1) for each Razor view (*.cshtml).
/// </summary>
/// <remarks>
/// This middleware allows for dynamic content generation in Razor Pages by leveraging PowerShell scripts.
/// Middleware that lets any Razor view (*.cshtml) load a sibling PowerShell
/// script (*.cshtml.ps1) in the SAME request.  The script can set `$Model` which
/// then becomes available to the Razor page through HttpContext.Items.
/// -----------------------------------------------------------------------------
///
/// Usage (inside KestrunHost.ApplyConfiguration):
///     builder.Services.AddRazorPages();                    // already present
///     …
/// /*  AFTER you build App and create _runspacePool:  */
///
///     App.UsePowerShellRazorPages(_runspacePool);
///
/// That’s it – no per-page registration.
/// </remarks>
public static class PowerShellRazorPage
{
    private const string MODEL_KEY = "PageModel";

    /// <summary>
    /// Enables <c>.cshtml</c> + <c>.cshtml.ps1</c> pairs.
    /// For a request to <c>/Foo</c> it will, in order:
    /// <list type="number">
    ///   <item><description>Look for <c>Pages/Foo.cshtml</c></description></item>
    ///   <item><description>If a <c>Pages/Foo.cshtml.ps1</c> exists, execute it
    ///       in the supplied runspace-pool</description></item>
    ///   <item><description>Whatever the script assigns to <c>$Model</c>
    ///       is copied to <c>HttpContext.Items["PageModel"]</c></description></item>
    /// </list>
    /// Razor pages (or a generic <see cref="PwshKestrunModel"/>) can then
    /// read that dynamic object.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> pipeline.</param>
    /// <param name="pool">Kestrun’s shared <see cref="KestrunRunspacePoolManager"/>.</param>
    /// <returns><paramref name="app"/> for fluent chaining.</returns>
    public static IApplicationBuilder UsePowerShellRazorPages(
        this IApplicationBuilder app,
        KestrunRunspacePoolManager pool)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(pool);

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Configuring PowerShell Razor Pages middleware");
        }

        var env = app.ApplicationServices.GetRequiredService<IHostEnvironment>();
        var pagesRoot = Path.Combine(env.ContentRootPath, "Pages");
        Log.Information("Using Pages directory: {Path}", pagesRoot);
        if (!Directory.Exists(pagesRoot))
        {
            Log.Warning("Pages directory not found: {Path}", pagesRoot);
        }

        // MUST run before MapRazorPages()
        app.Use(async (context, next) =>
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Processing PowerShell Razor Page request for {Path}", context.Request.Path);
            }

            var relPath = GetRelativePath(context);
            if (relPath is null)
            {
                await next();
                return;
            }

            var (view, psfile, csfile) = BuildCandidatePaths(pagesRoot, relPath);
            if (HasCodeBehind(csfile))
            {
                await next();
                return;
            }

            if (!FilesExist(view, psfile))
            {
                await next();
                return;
            }

            PowerShell? ps = null;
            try
            {
                ps = CreatePowerShell(pool);
                PrepareSession(ps, context);
                await AddScriptFromFileAsync(ps, psfile, context.RequestAborted);
                LogExecution(psfile);
                var psResults = await InvokePowerShellAsync(ps).ConfigureAwait(false);
                LogResultsCount(psResults.Count);

                SetModelIfPresent(ps, context);

                if (HasErrors(ps))
                {
                    await HandleErrorsAsync(context, ps);
                    return;
                }

                LogStreamsIfAny(ps);

                await next(); // continue the pipeline
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("PowerShell Razor Page completed for {Path}", context.Request.Path);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred in PowerShell Razor Page middleware for {Path}", context.Request.Path);
            }
            finally
            {
                ReturnRunspaceAndDispose(ps, pool);
            }
        });

        // static files & routing can be added earlier in pipeline

        app.UseRouting();
        app.UseEndpoints(e => e.MapRazorPages());
        return app;
    }
    /// <summary>
    /// Gets the relative path for the PowerShell Razor Page from the HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The relative path for the PowerShell Razor Page.</returns>
    private static string? GetRelativePath(HttpContext context)
    {
        var relPath = context.Request.Path.Value?.Trim('/');
        if (string.IsNullOrEmpty(relPath))
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Request path is empty, skipping PowerShell Razor Page processing");
            }

            return null;
        }
        relPath = relPath.Replace('/', Path.DirectorySeparatorChar);
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Transformed request path to relative: {RelPath}", relPath);
        }

        return relPath;
    }

    /// <summary>
    /// Builds the candidate file paths for a PowerShell Razor Page.
    /// </summary>
    /// <param name="pagesRoot">The root directory for the Razor Pages.</param>
    /// <param name="relPath">The relative path for the Razor Page.</param>
    /// <returns>The candidate file paths for the Razor Page.</returns>
    private static (string view, string psfile, string csfile) BuildCandidatePaths(string pagesRoot, string relPath)
    {
        var view = Path.Combine(pagesRoot, relPath + ".cshtml");
        var psfile = view + ".ps1";
        var csfile = view + ".cs";
        return (view, psfile, csfile);
    }

    /// <summary>
    /// Checks if the C# code-behind file exists.
    /// </summary>
    /// <param name="csfile">The path to the C# code-behind file.</param>
    /// <returns>True if the code-behind file exists; otherwise, false.</returns>
    private static bool HasCodeBehind(string csfile)
    {
        if (File.Exists(csfile))
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Found C# code-behind file: {CsFile}", csfile);
            }

            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if the PowerShell Razor Page files exist.
    /// </summary>
    /// <param name="view">The path to the Razor view file.</param>
    /// <param name="psfile">The path to the PowerShell script file.</param>
    /// <returns>True if the files exist; otherwise, false.</returns>
    private static bool FilesExist(string view, string psfile)
    {
        var ok = File.Exists(view) && File.Exists(psfile);
        if (!ok && Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("PowerShell Razor Page files not found: {View} or {PsFile}", view, psfile);
        }

        return ok;
    }

    /// <summary>
    /// Creates a PowerShell instance from the runspace pool.
    /// </summary>
    /// <param name="pool">The runspace pool manager.</param>
    /// <returns>The PowerShell instance.</returns>
    private static PowerShell CreatePowerShell(KestrunRunspacePoolManager pool)
        => PowerShell.Create(pool.Acquire());

    /// <summary>
    /// Prepares the PowerShell session with the HTTP context.
    /// </summary>
    /// <param name="ps">The PowerShell instance.</param>
    /// <param name="context">The HTTP context.</param>
    private static void PrepareSession(PowerShell ps, HttpContext context)
    {
        var ss = ps.Runspace.SessionStateProxy;
        ss.SetVariable("Context", context);
        ss.SetVariable("Model", null);
    }

    /// <summary>
    /// Adds a PowerShell script from a file to the PowerShell instance.
    /// </summary>
    /// <param name="ps">The PowerShell instance.</param>
    /// <param name="path">The path to the script file.</param>
    /// <param name="token">The cancellation token.</param>
    private static async Task AddScriptFromFileAsync(PowerShell ps, string path, CancellationToken token)
    {
        var script = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);
        ps.AddScript(script);
    }

    /// <summary>
    /// Logs the execution of a PowerShell script.
    /// </summary>
    /// <param name="psfile">The path to the PowerShell script file.</param>
    private static void LogExecution(string psfile)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Executing PowerShell script: {ScriptFile}", psfile);
        }
    }

    private static Task<System.Management.Automation.PSDataCollection<System.Management.Automation.PSObject>> InvokePowerShellAsync(PowerShell ps)
        => ps.InvokeAsync();

    private static void LogResultsCount(int count)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("PowerShell script returned {Count} results", count);
        }
    }

    private static void SetModelIfPresent(PowerShell ps, HttpContext context)
    {
        var model = ps.Runspace.SessionStateProxy.GetVariable("Model");
        if (model is not null)
        {
            context.Items[MODEL_KEY] = model;
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("PowerShell Razor Page model set: {Model}", model);
        }
    }

    private static bool HasErrors(PowerShell ps) => ps.HadErrors || ps.Streams.Error.Count != 0;

    private static async Task HandleErrorsAsync(HttpContext context, PowerShell ps)
    {
        Log.Error("PowerShell script encountered errors: {ErrorCount}", ps.Streams.Error.Count);
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("PowerShell script errors: {Errors}", BuildError.Text(ps));
        }

        await BuildError.ResponseAsync(context, ps);
    }

    private static void LogStreamsIfAny(PowerShell ps)
    {
        if (ps.Streams.Verbose.Count > 0 || ps.Streams.Debug.Count > 0 || ps.Streams.Warning.Count > 0 || ps.Streams.Information.Count > 0)
        {
            Log.Verbose("PowerShell script completed with verbose/debug/warning/info messages.");
            Log.Verbose(BuildError.Text(ps));
        }
        else if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("PowerShell script completed without errors or messages.");
        }
    }

    private static void ReturnRunspaceAndDispose(PowerShell? ps, KestrunRunspacePoolManager pool)
    {
        if (ps is null)
        {
            return;
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Returning runspace to pool: {RunspaceId}", ps.Runspace.InstanceId);
        }

        pool.Release(ps.Runspace);
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Disposing PowerShell instance: {InstanceId}", ps.InstanceId);
        }

        ps.Dispose();
    }
}
