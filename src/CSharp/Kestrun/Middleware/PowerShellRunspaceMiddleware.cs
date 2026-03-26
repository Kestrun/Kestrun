using System.Management.Automation;
using System.Diagnostics;
using System.Management.Automation.Runspaces;
using Kestrun.Languages;
using Kestrun.Models;
using Kestrun.Scripting;
using Serilog.Events;
using Kestrun.Hosting;
using Kestrun.Logging;

namespace Kestrun.Middleware;

/// <summary>
/// Initializes a new instance of the <see cref="PowerShellRunspaceMiddleware"/> class.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="pool">The runspace pool manager.</param>
public sealed class PowerShellRunspaceMiddleware(RequestDelegate next, KestrunRunspacePoolManager pool)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly KestrunRunspacePoolManager _pool = pool ?? throw new ArgumentNullException(nameof(pool));
    private static int _inFlight; // diagnostic concurrency counter

    private KestrunHost Host => _pool.Host;
    private Serilog.ILogger Log => Host.Logger;

    /// <summary>
    /// Processes an HTTP request using a PowerShell runspace from the pool.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var start = DateTime.UtcNow;
        var current = BeginRequestDiagnostics(context, start);
        Runspace? runspace = null;
        PowerShell? ps = null;
        var cleanupTransferredToResponse = false;

        try
        {
            LogMiddlewareStarted(context);
            runspace = await AcquireRunspaceAsync(context, current);
            ps = CreatePowerShellInstance(runspace);
            InitializeRequestContext(context, ps);
            RegisterDeferredCleanup(context, ps, runspace);
            cleanupTransferredToResponse = true;

            LogPipelineContinuation(context);
            await _next(context); // continue the pipeline
            LogMiddlewareCompleted(context);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred in PowerShellRunspaceMiddleware");
            throw; // allow ExceptionHandler to catch and handle (re-exec or JSON)
        }
        finally
        {
            if (!cleanupTransferredToResponse)
            {
                CleanupRequestResources(context, ps, runspace);
            }

            CompleteRequestDiagnostics(context, start);
        }
    }

    /// <summary>
    /// Records the initial request diagnostics and returns the current in-flight count.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="start">The request start time in UTC.</param>
    /// <returns>The number of requests currently in flight.</returns>
    private int BeginRequestDiagnostics(HttpContext context, DateTime start)
    {
        var current = Interlocked.Increment(ref _inFlight);
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.DebugSanitized("ENTER InvokeAsync path={Path} inFlight={InFlight} thread={Thread} time={Start}",
                context.Request.Path, current, Environment.CurrentManagedThreadId, start.ToString("O"));
        }

        return current;
    }

    /// <summary>
    /// Logs the middleware entry for the current request when debug logging is enabled.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    private void LogMiddlewareStarted(HttpContext context)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.DebugSanitized("PowerShellRunspaceMiddleware started for {Path}", context.Request.Path);
        }
    }

    /// <summary>
    /// Acquires a runspace for the request and logs the acquisition duration.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="inFlight">The current in-flight request count.</param>
    /// <returns>The acquired runspace.</returns>
    private async Task<Runspace> AcquireRunspaceAsync(HttpContext context, int inFlight)
    {
        var acquireStart = Stopwatch.GetTimestamp();
        var runspace = await _pool.AcquireAsync(context.RequestAborted);
        var acquireMs = (Stopwatch.GetTimestamp() - acquireStart) * 1000.0 / Stopwatch.Frequency;
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.DebugSanitized("Runspace acquired for {Path} in {AcquireMs} ms (inFlight={InFlight})", context.Request.Path, acquireMs, inFlight);
        }

        return runspace;
    }

    /// <summary>
    /// Creates a PowerShell instance bound to the provided runspace.
    /// </summary>
    /// <param name="runspace">The runspace assigned to the current request.</param>
    /// <returns>A PowerShell instance that uses the provided runspace.</returns>
    private static PowerShell CreatePowerShellInstance(Runspace runspace)
    {
        var ps = PowerShell.Create();
        ps.Runspace = runspace;
        return ps;
    }

    /// <summary>
    /// Initializes the request-specific PowerShell and Kestrun context state.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="ps">The PowerShell instance serving the request.</param>
    private void InitializeRequestContext(HttpContext context, PowerShell ps)
    {
        context.Items[PowerShellDelegateBuilder.PS_INSTANCE_KEY] = ps;

        var kestrunContext = new KestrunContext(Host, context);
        context.Items[PowerShellDelegateBuilder.KR_CONTEXT_KEY] = kestrunContext;

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.DebugSanitized("PowerShellRunspaceMiddleware - Setting KestrunContext in HttpContext.Items for {Path}", context.Request.Path);
        }

        Log.Verbose("Setting PowerShell variables for Request and Response in the runspace.");
        var sessionState = ps.Runspace.SessionStateProxy;
        sessionState.SetVariable("Context", kestrunContext);

        if (context.Items.TryGetValue("KrLocalizer", out var localizer))
        {
            sessionState.SetVariable("Localizer", localizer);
        }
    }

    /// <summary>
    /// Registers response completion cleanup so the runspace remains available to later middleware.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="ps">The PowerShell instance serving the request.</param>
    /// <param name="runspace">The runspace serving the request.</param>
    private void RegisterDeferredCleanup(HttpContext context, PowerShell ps, Runspace runspace)
    {
        context.Response.OnCompleted(() =>
        {
            CleanupPowerShellInstance(ps, "OnCompleted: Error disposing PowerShell instance", "OnCompleted: Disposing PowerShell instance: {InstanceId}");
            ReleaseRunspace(runspace, "OnCompleted: Error returning runspace to pool", "OnCompleted: Returning runspace to pool: {RunspaceId} {name} {id}");
            ClearRequestItems(context);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Logs that the middleware is continuing to the next pipeline component.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    private void LogPipelineContinuation(HttpContext context)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.DebugSanitized("PowerShellRunspaceMiddleware - Continuing Pipeline  for {Path}", context.Request.Path);
        }
    }

    /// <summary>
    /// Logs successful middleware completion for the current request.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    private void LogMiddlewareCompleted(HttpContext context)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.DebugSanitized("PowerShellRunspaceMiddleware completed for {Path}", context.Request.Path);
        }
    }

    /// <summary>
    /// Cleans up request resources immediately when response completion cleanup was not registered.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="ps">The PowerShell instance serving the request.</param>
    /// <param name="runspace">The runspace serving the request.</param>
    private void CleanupRequestResources(HttpContext context, PowerShell? ps, Runspace? runspace)
    {
        CleanupPowerShellInstance(ps, "Error disposing PowerShell instance during middleware cleanup");
        ReleaseRunspace(runspace, "Error returning runspace to pool during middleware cleanup");
        ClearRequestItems(context);
    }

    /// <summary>
    /// Disposes the PowerShell instance with debug-level error handling.
    /// </summary>
    /// <param name="ps">The PowerShell instance to dispose.</param>
    /// <param name="errorMessage">The message to log if disposal fails.</param>
    /// <param name="successMessageTemplate">An optional debug log template used before disposal.</param>
    private void CleanupPowerShellInstance(PowerShell? ps, string errorMessage, string? successMessageTemplate = null)
    {
        try
        {
            if (ps is null)
            {
                return;
            }

            if (successMessageTemplate is not null && Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(successMessageTemplate, ps.InstanceId);
            }

            ps.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, errorMessage);
        }
    }

    /// <summary>
    /// Returns the runspace to the pool with debug-level error handling.
    /// </summary>
    /// <param name="runspace">The runspace to release.</param>
    /// <param name="errorMessage">The message to log if release fails.</param>
    /// <param name="successMessageTemplate">An optional debug log template used before release.</param>
    private void ReleaseRunspace(Runspace? runspace, string errorMessage, string? successMessageTemplate = null)
    {
        try
        {
            if (runspace is null)
            {
                return;
            }

            if (successMessageTemplate is not null && Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(successMessageTemplate, runspace.InstanceId, runspace.Name, runspace.Id);
            }

            _pool.Release(runspace);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, errorMessage);
        }
    }

    /// <summary>
    /// Removes request-scoped middleware state from the HTTP context.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    private static void ClearRequestItems(HttpContext context)
    {
        _ = context.Items.Remove(PowerShellDelegateBuilder.PS_INSTANCE_KEY);
        _ = context.Items.Remove(PowerShellDelegateBuilder.KR_CONTEXT_KEY);
    }

    /// <summary>
    /// Records the final request diagnostics after the middleware finishes processing.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="start">The request start time in UTC.</param>
    private void CompleteRequestDiagnostics(HttpContext context, DateTime start)
    {
        var remaining = Interlocked.Decrement(ref _inFlight);
        var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.DebugSanitized("PowerShellRunspaceMiddleware ended for {Path} durationMs={durationMs} inFlight={remaining}",
                context.Request.Path, durationMs, remaining);
        }
    }
}
