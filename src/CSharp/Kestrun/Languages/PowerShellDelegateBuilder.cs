using System.Management.Automation;
using Kestrun.Logging;
using Kestrun.Models;
using Kestrun.Utilities;
using Serilog.Events;

namespace Kestrun.Languages;

internal static class PowerShellDelegateBuilder
{
    public const string PS_INSTANCE_KEY = "PS_INSTANCE";
    public const string KR_CONTEXT_KEY = "KR_CONTEXT";

    internal static RequestDelegate Build(string code, Serilog.ILogger log, Dictionary<string, object?>? arguments)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Building PowerShell delegate, script length={Length}", code.Length);
        }

        return async context =>
        {
            if (log.IsEnabled(LogEventLevel.Debug))
            {
                log.DebugSanitized("PS delegate invoked for {Path}", context.Request.Path);
            }

            var ps = GetPowerShellFromContext(context, log);
            // Ensure the runspace pool is open before executing the script
            try
            {
                PowerShellExecutionHelpers.SetVariables(ps, arguments, log);

                log.Verbose("Setting PowerShell variables for Request and Response in the runspace.");
                var krContext = GetKestrunContext(context);

                PowerShellExecutionHelpers.AddScript(ps, code);
                var psResults = await PowerShellExecutionHelpers.InvokeAsync(ps, log, context.RequestAborted).ConfigureAwait(false);
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

                log.Verbose("Applying response to HttpResponse...");
                await ApplyResponseAsync(context, krContext).ConfigureAwait(false);
            }
            // optional: catch client cancellation to avoid noisy logs
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // client disconnected – nothing to send
            }
            catch (Exception ex)
            {
                // Log the exception (optional)
                log.Error(ex, "PowerShell script failed - {Preview}", code[..Math.Min(40, code.Length)]);
                context.Response.StatusCode = 500; // Internal Server Error
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync("An error occurred while processing your request.");
            }
            finally
            {
                await CompleteResponseSafelyAsync(context, log).ConfigureAwait(false);
            }
        };
    }

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

    private static KestrunContext GetKestrunContext(HttpContext context)
        => context.Items[KR_CONTEXT_KEY] as KestrunContext
           ?? throw new InvalidOperationException($"{KR_CONTEXT_KEY} key not found in context items.");

    // Removed duplicated SetArguments/AddScript/Invoke methods (now in PowerShellExecutionHelpers)

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

    private static async Task<bool> HandleErrorsIfAnyAsync(HttpContext context, PowerShell ps)
    {
        if (ps.HadErrors || ps.Streams.Error.Count != 0)
        {
            await BuildError.ResponseAsync(context, ps).ConfigureAwait(false);
            return true;
        }
        return false;
    }

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

    private static async Task CompleteResponseSafelyAsync(HttpContext context, Serilog.ILogger log)
    {
        // CompleteAsync is idempotent – safe to call once more
        try
        {
            log.Verbose("Completing response for " + context.Request.Path);
            await context.Response.CompleteAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException odex)
        {
            // This can happen if the response has already been completed
            // or the client has disconnected
            log.DebugSanitized(odex, "Response already completed for {Path}", context.Request.Path);
        }
        catch (InvalidOperationException ioex)
        {
            // This can happen if the response has already been completed
            log.DebugSanitized(ioex, "Response already completed for {Path}", context.Request.Path);
            // No action needed, as the response is already completed
        }
    }
}
