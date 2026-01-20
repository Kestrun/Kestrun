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

    /// <summary>
    /// Builds a RequestDelegate that executes the given PowerShell script code.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="code">The PowerShell script code to execute.</param>
    /// <param name="arguments">Arguments to inject as variables into the script.</param>
    /// <returns>A delegate that handles HTTP requests.</returns>
    internal static RequestDelegate Build(KestrunHost host, string code, Dictionary<string, object?>? arguments)
    {
        var log = host.Logger;
        ArgumentNullException.ThrowIfNull(code);
        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("Building PowerShell delegate, script length={Length}", code.Length);
        }
        // Build and return the execution delegate
        return BuildExecutionDelegate(host, code, arguments);
    }

    /// <summary>
    /// Builds the execution delegate that runs the PowerShell script.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="code">The PowerShell script code to execute.</param>
    /// <param name="arguments">Arguments to inject as variables into the script.</param>
    /// <returns>A delegate that handles HTTP requests.</returns>
    private static RequestDelegate BuildExecutionDelegate(KestrunHost host, string code, Dictionary<string, object?>? arguments)
        => context => ExecutePowerShellRequestAsync(host, code, arguments, context);

    /// <summary>
    /// Executes the PowerShell request pipeline and applies the resulting response.
    /// </summary>
    /// <param name="host">Kestrun host instance.</param>
    /// <param name="code">PowerShell script text.</param>
    /// <param name="arguments">Optional variables to inject into the script.</param>
    /// <param name="context">Current HTTP context.</param>
    private static async Task ExecutePowerShellRequestAsync(
        KestrunHost host,
        string code,
        Dictionary<string, object?>? arguments,
        HttpContext context)
    {
        // Get the logger for this request
        var log = host.Logger;

        if (log.IsEnabled(LogEventLevel.Debug))
        {
            log.DebugSanitized("PS delegate invoked for {Path}", context.Request.Path);
        }

        KestrunContext? krContext = null;
        // Retrieve the PowerShell instance from the context items
        var ps = GetPowerShellFromContext(context, log);

        try
        {
            // Execute the script and apply the response
            krContext = await ExecuteScriptAndApplyResponseAsync(
                context,
                ps,
                code,
                arguments,
                log).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // client disconnected – nothing to send
        }
        catch (ParameterBindingException pbaex)
        {
            // Handle PowerShell parameter binding errors
            await HandleParameterBindingExceptionAsync(context, pbaex, code, log).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Handle unexpected exceptions
            await HandleUnexpectedExceptionAsync(context, ex, krContext, code, log).ConfigureAwait(false);
        }
        finally
        {
            // Do not call Response.CompleteAsync here; leaving the response open allows
            // downstream middleware like StatusCodePages to generate a body for status-only responses.
        }
    }

    /// <summary>
    /// Executes the PowerShell script for a request and applies Kestrun's response model to the HTTP response.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    /// <param name="ps">PowerShell instance for the request.</param>
    /// <param name="code">PowerShell script code.</param>
    /// <param name="arguments">Optional injected variables.</param>
    /// <param name="log">Logger instance.</param>
    /// <returns>The resolved <see cref="KestrunContext"/> for the request.</returns>
    private static async Task<KestrunContext> ExecuteScriptAndApplyResponseAsync(
        HttpContext context,
        PowerShell ps,
        string code,
        Dictionary<string, object?>? arguments,
        Serilog.ILogger log)
    {
        // Check if verbose logging is enabled
        var isVerboseEnabled = log.IsEnabled(LogEventLevel.Verbose);
        // Set up variables in the PowerShell runspace
        PowerShellExecutionHelpers.SetVariables(ps, arguments, log);
        if (isVerboseEnabled)
        {
            log.Verbose("Setting PowerShell variables for Request and Response in the runspace.");
        }
        // Retrieve the KestrunContext for this request
        var krContext = GetKestrunContext(context);

        PowerShellExecutionHelpers.AddScript(ps, code);

        // Extract and add parameters for injection
        ParameterForInjectionInfo.InjectParameters(krContext, ps);

        if (isVerboseEnabled)
        {
            log.Verbose("Invoking PowerShell script...");
        }
        // Invoke the PowerShell script
        var psResults = await ps.InvokeAsync(log, context.RequestAborted).ConfigureAwait(false);
        LogTopResults(log, psResults);
        // Handle any errors from the script execution
        if (await HandleErrorsIfAnyAsync(context, ps).ConfigureAwait(false))
        {
            return krContext;
        }
        // Log any side-channel messages from the script
        LogSideChannelMessagesIfAny(log, ps);
        // Check for redirects in the KestrunContext response
        if (HandleRedirectIfAny(context, krContext, log))
        {
            return krContext;
        }

        if (context.Response.HasStarted)
        {
            if (isVerboseEnabled)
            {
                log.Verbose("HttpResponse has already started; skipping KestrunResponse.ApplyTo().");
            }
            return krContext;
        }

        if (isVerboseEnabled)
        {
            log.Verbose("No redirect detected; applying response to HttpResponse...");
        }
        // Apply the KestrunResponse to the HttpResponse
        await ApplyResponseAsync(context, krContext).ConfigureAwait(false);

        return krContext;
    }

    /// <summary>
    /// Handles PowerShell parameter binding errors by logging diagnostics and returning a 400 response.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    /// <param name="exception">The exception thrown by PowerShell.</param>
    /// <param name="code">PowerShell script code.</param>
    /// <param name="log">Logger instance.</param>
    private static async Task HandleParameterBindingExceptionAsync(
        HttpContext context,
        ParameterBindingException exception,
        string code,
        Serilog.ILogger log)
    {
        var fqid = exception.ErrorRecord?.FullyQualifiedErrorId;
        var cat = exception.ErrorRecord?.CategoryInfo?.Category;
        var errMsg = exception.ErrorRecord?.Exception?.Message ?? exception.Message;
        var errRecordText = exception.ErrorRecord?.ToString();

        log.Error(
            "PowerShell parameter binding error ({Category}/{FQID}) - {Message} - {Preview}",
            cat,
            fqid,
            errMsg,
            code[..Math.Min(80, code.Length)]);

        if (!string.IsNullOrWhiteSpace(errRecordText) && log.IsEnabled(LogEventLevel.Debug))
        {
            log.Debug("PowerShell parameter binding error record: {ErrorRecord}", errRecordText);
        }

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync("Invalid request parameters.").ConfigureAwait(false);
    }

    /// <summary>
    /// Handles unexpected exceptions during script execution, either returning a generic 500 or rethrowing
    /// when exception middleware options are configured.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    /// <param name="exception">Thrown exception.</param>
    /// <param name="krContext">Resolved KestrunContext when available.</param>
    /// <param name="code">PowerShell script code.</param>
    /// <param name="log">Logger instance.</param>
    private static async Task HandleUnexpectedExceptionAsync(
        HttpContext context,
        Exception exception,
        KestrunContext? krContext,
        string code,
        Serilog.ILogger log)
    {
        if (krContext?.Host?.ExceptionOptions is not null)
        {
            throw exception;
        }

        log.Error(exception, "PowerShell script failed - {Preview}", code[..Math.Min(40, code.Length)]);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync("An error occurred while processing your request.").ConfigureAwait(false);
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
