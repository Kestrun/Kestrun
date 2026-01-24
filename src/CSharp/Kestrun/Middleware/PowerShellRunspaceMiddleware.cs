using System.Management.Automation;
using System.Diagnostics;
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
        // Concurrency diagnostics
        var start = DateTime.UtcNow;
        var threadId = Environment.CurrentManagedThreadId;
        var current = Interlocked.Increment(ref _inFlight);
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.DebugSanitized("ENTER InvokeAsync path={Path} inFlight={InFlight} thread={Thread} time={Start}",
                context.Request.Path, current, threadId, start.ToString("O"));
        }

        try
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.DebugSanitized("PowerShellRunspaceMiddleware started for {Path}", context.Request.Path);
            }

            // Acquire a runspace from the pool asynchronously (avoid blocking thread pool while waiting)
            var acquireStart = Stopwatch.GetTimestamp();
            var runspace = await _pool.AcquireAsync(context.RequestAborted);
            var acquireMs = (Stopwatch.GetTimestamp() - acquireStart) * 1000.0 / Stopwatch.Frequency;
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.DebugSanitized("Runspace acquired for {Path} in {AcquireMs} ms (inFlight={InFlight})", context.Request.Path, acquireMs, current);
            }

            var ps = PowerShell.Create();
            ps.Runspace = runspace;

            // Store the PowerShell instance in the context for later use
            context.Items[PowerShellDelegateBuilder.PS_INSTANCE_KEY] = ps;

            var kestrunContext = new KestrunContext(Host, context);
            // Set the KestrunContext in the HttpContext.Items for later use
            context.Items[PowerShellDelegateBuilder.KR_CONTEXT_KEY] = kestrunContext;

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.DebugSanitized("PowerShellRunspaceMiddleware - Setting KestrunContext in HttpContext.Items for {Path}", context.Request.Path);
            }

            Log.Verbose("Setting PowerShell variables for Request and Response in the runspace.");
            // Set the PowerShell variables for the request and response
            var ss = ps.Runspace.SessionStateProxy;
            ss.SetVariable("Context", kestrunContext);

            if (context.Items.TryGetValue("KrLocalizer", out var localizer))
            {
                ss.SetVariable("Localizer", localizer);
            }

            // Defer cleanup until the response is fully completed. This ensures
            // post-endpoint middleware (e.g., StatusCodePages) can still access the runspace.
            context.Response.OnCompleted(() =>
            {
                try
                {
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug("OnCompleted: Returning runspace to pool: {RunspaceId} {name} {id}", ps.Runspace.InstanceId, ps.Runspace.Name, ps.Runspace.Id);
                    }
                    _pool.Release(ps.Runspace);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "OnCompleted: Error returning runspace to pool");
                }
                finally
                {
                    try
                    {
                        if (Log.IsEnabled(LogEventLevel.Debug))
                        {
                            Log.Debug("OnCompleted: Disposing PowerShell instance: {InstanceId}", ps.InstanceId);
                        }
                        ps.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "OnCompleted: Error disposing PowerShell instance");
                    }
                    _ = context.Items.Remove(PowerShellDelegateBuilder.PS_INSTANCE_KEY);
                    _ = context.Items.Remove(PowerShellDelegateBuilder.KR_CONTEXT_KEY);
                }
                return Task.CompletedTask;
            });

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.DebugSanitized("PowerShellRunspaceMiddleware - Continuing Pipeline  for {Path}", context.Request.Path);
            }

            await _next(context); // continue the pipeline
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.DebugSanitized("PowerShellRunspaceMiddleware completed for {Path}", context.Request.Path);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred in PowerShellRunspaceMiddleware");
            throw; // allow ExceptionHandler to catch and handle (re-exec or JSON)
        }
        finally
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
}
