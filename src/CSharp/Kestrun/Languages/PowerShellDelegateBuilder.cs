using System.Management.Automation;
using Kestrun.Hosting;
using Kestrun.Logging;
using Kestrun.Models;
using Kestrun.Utilities;
using Serilog.Events;

namespace Kestrun.Languages;

internal static class PowerShellDelegateBuilder
{
    public const string PS_INSTANCE_KEY = "PS_INSTANCE";
    public const string KR_CONTEXT_KEY = "KR_CONTEXT";

    internal static RequestDelegate Build(KestrunHost host, string code, Dictionary<string, object?>? arguments)
    {
        var log = host.Logger;
        ArgumentNullException.ThrowIfNull(code);
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Building PowerShell delegate, script length={Length}", code.Length);
        }

        return async context =>
        {
            // Log invocation
            if (log.IsEnabled(LogEventLevel.Debug))
            {
                log.DebugSanitized("PS delegate invoked for {Path}", context.Request.Path);
            }
            // Prepare for execution
            KestrunContext? krContext = null;
            // Get the PowerShell instance from the context (set by middleware)
            var ps = GetPowerShellFromContext(context, log);

            // Ensure the runspace pool is open before executing the script
            try
            {
                PowerShellExecutionHelpers.SetVariables(ps, arguments, log);
                if (log.IsEnabled(LogEventLevel.Verbose))
                {
                    log.Verbose("Setting PowerShell variables for Request and Response in the runspace.");
                }
                krContext = GetKestrunContext(context);

                PowerShellExecutionHelpers.AddScript(ps, code);

                // Extract and add parameters for injection
                ParameterForInjectionInfo.InjectParameters(krContext, ps);

                // Execute the script
                if (log.IsEnabled(LogEventLevel.Verbose))
                {
                    log.Verbose("Invoking PowerShell script...");
                }
                var psResults = await ps.InvokeAsync(log, context.RequestAborted).ConfigureAwait(false);
                LogTopResults(log, psResults);

                if (await HandleErrorsIfAnyAsync(context, ps).ConfigureAwait(false))
                {
                    return;
                }

                LogSideChannelMessagesIfAny(log, ps);

                if (HandleRedirectIfAny(context, krContext, log))
                {
                    return;
                }

                // Some endpoints (e.g., SSE streaming) write directly to the HttpResponse and
                // intentionally start the response early. In that case, applying KestrunResponse
                // would attempt to set headers/status again and throw.
                if (context.Response.HasStarted)
                {
                    if (log.IsEnabled(LogEventLevel.Verbose))
                    {
                        log.Verbose("HttpResponse has already started; skipping KestrunResponse.ApplyTo().");
                    }
                    return;
                }
                if (log.IsEnabled(LogEventLevel.Verbose))
                {
                    log.Verbose("No redirect detected; applying response to HttpResponse...");
                }
                await ApplyResponseAsync(context, krContext).ConfigureAwait(false);
            }
            // optional: catch client cancellation to avoid noisy logs
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // client disconnected – nothing to send
            }
            catch (ParameterBindingException pbaex)
            {
                var fqid = pbaex.ErrorRecord?.FullyQualifiedErrorId;
                var cat = pbaex.ErrorRecord?.CategoryInfo?.Category;
                // Log parameter binding errors with preview of code
                log.Error("PowerShell parameter binding error ({Category}/{FQID}) - {Preview}",
                    cat, fqid, code[..Math.Min(40, code.Length)]);
                // Return 400 Bad Request for parameter binding errors
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync("Invalid request parameters.");
            }
            catch (Exception ex)
            {
                // If we have exception options, set a 500 status code and generic message.
                // Otherwise rethrow to let higher-level middleware handle it (e.g., Developer Exception Page
                if (krContext?.Host?.ExceptionOptions is null)
                { // Log and handle script errors
                    log.Error(ex, "PowerShell script failed - {Preview}", code[..Math.Min(40, code.Length)]);
                    context.Response.StatusCode = 500; // Internal Server Error
                    context.Response.ContentType = "text/plain; charset=utf-8";
                    await context.Response.WriteAsync("An error occurred while processing your request.");
                }
                else
                {
                    // re-throw to let higher-level middleware handle it (e.g., Developer Exception Page)
                    throw;
                }
            }
            finally
            {
                // Do not call Response.CompleteAsync here; leaving the response open allows
                // downstream middleware like StatusCodePages to generate a body for status-only responses.
            }
        };
    }

    /// <summary>
    /// Retrieves the PowerShell instance from the HttpContext items.
    /// </summary>
    /// <param name="context">The HttpContext from which to retrieve the PowerShell instance.</param>
    /// <param name="log">The logger to use for logging.</param>
    /// <returns>The PowerShell instance associated with the current request.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the PowerShell instance is not found in the context items.</exception>
    private static PowerShell GetPowerShellFromContext(HttpContext context, Serilog.ILogger log)
    {
        if (!context.Items.ContainsKey(PS_INSTANCE_KEY))
        {
            throw new InvalidOperationException("PowerShell runspace not found in context items. Ensure PowerShellRunspaceMiddleware is registered.");
        }

        log.Verbose("Retrieving PowerShell instance from context items.");
        var ps = context.Items[PS_INSTANCE_KEY] as PowerShell
                 ?? throw new InvalidOperationException("PowerShell instance not found in context items.");
        return ps.Runspace == null
            ? throw new InvalidOperationException("PowerShell runspace is not set. Ensure PowerShellRunspaceMiddleware is registered.")
            : ps;
    }

    /// <summary>
    /// Retrieves the KestrunContext from the HttpContext items.
    /// </summary>
    /// <param name="context">The HttpContext from which to retrieve the KestrunContext.</param>
    /// <returns>The KestrunContext associated with the current request.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the KestrunContext is not found in the context items.</exception>
    private static KestrunContext GetKestrunContext(HttpContext context)
        => context.Items[KR_CONTEXT_KEY] as KestrunContext
           ?? throw new InvalidOperationException($"{KR_CONTEXT_KEY} key not found in context items.");

    ///<summary>
    /// Logs the top results from the PowerShell script output for debugging purposes.
    /// Only logs if the log level is set to Debug.
    /// </summary>
    /// <param name="log">The logger to use for logging.</param>
    /// <param name="psResults">The collection of PSObject results from the PowerShell script.</param>
    private static void LogTopResults(Serilog.ILogger log, PSDataCollection<PSObject> psResults)
    {
        if (!log.IsEnabled(LogEventLevel.Debug))
        {
            return;
        }

        log.Debug("PowerShell script output:");
        foreach (var r in psResults.Take(10))
        {
            log.Debug("   • {Result}", r);
        }
        if (psResults.Count > 10)
        {
            log.Debug("   … {Count} more", psResults.Count - 10);
        }
    }

    /// <summary>
    /// Handles any errors that occurred during the PowerShell script execution.
    /// </summary>
    /// <param name="context">The HttpContext for the current request.</param>
    /// <param name="ps">The PowerShell instance used for script execution.</param>
    /// <returns>True if errors were handled, false otherwise.</returns>
    private static async Task<bool> HandleErrorsIfAnyAsync(HttpContext context, PowerShell ps)
    {
        if (ps.HadErrors || ps.Streams.Error.Count != 0)
        {
            await BuildError.ResponseAsync(context, ps).ConfigureAwait(false);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Logs any side-channel messages (Verbose, Debug, Warning, Information) produced by the PowerShell script.
    /// </summary>
    /// <param name="log">The logger to use for logging.</param>
    /// <param name="ps">The PowerShell instance used to invoke the script.</param>
    private static void LogSideChannelMessagesIfAny(Serilog.ILogger log, PowerShell ps)
    {
        if (ps.Streams.Verbose.Count > 0 || ps.Streams.Debug.Count > 0 || ps.Streams.Warning.Count > 0 || ps.Streams.Information.Count > 0)
        {
            log.Verbose("PowerShell script completed with verbose/debug/warning/info messages.");
            log.Verbose(BuildError.Text(ps));
        }
        log.Verbose("PowerShell script completed successfully.");
    }

    private static bool HandleRedirectIfAny(HttpContext context, KestrunContext krContext, Serilog.ILogger log)
    {
        if (!string.IsNullOrEmpty(krContext.Response.RedirectUrl))
        {
            log.Verbose($"Redirecting to {krContext.Response.RedirectUrl}");
            context.Response.Redirect(krContext.Response.RedirectUrl);
            return true;
        }
        return false;
    }

    private static Task ApplyResponseAsync(HttpContext context, KestrunContext krContext)
        => krContext.Response.ApplyTo(context.Response);

    // Removed explicit Response.CompleteAsync to allow StatusCodePages to run after endpoints when appropriate.
}
