using System.Management.Automation;
using System.Net.Http.Headers;
using Kestrun.Hosting;
using Kestrun.KrException;
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

        return context => ExecutePowerShellRequestAsync(context, log, code, arguments);
    }

    /// <summary>
    /// Executes the PowerShell request pipeline and applies the resulting response.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    /// <param name="log">Logger instance.</param>
    /// <param name="code">PowerShell script code.</param>
    /// <param name="arguments">Arguments to inject as variables into the script.</param>
    private static async Task ExecutePowerShellRequestAsync(
        HttpContext context,
        Serilog.ILogger log,
        string code,
        Dictionary<string, object?>? arguments)
    {
        var isLogVerbose = log.IsEnabled(LogEventLevel.Verbose);
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
            if (isLogVerbose)
            {
                log.Verbose("Setting PowerShell variables for Request and Response in the runspace.");
            }
            krContext = GetKestrunContext(context);

            var allowed = krContext.MapRouteOptions.AllowedRequestContentTypes;

            if (allowed is { Count: > 0 })
            {
                // Reliable body detection
                var hasBody =
                    (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > 0) ||
                    context.Request.Headers.TransferEncoding.Count > 0;

                if (string.IsNullOrWhiteSpace(context.Request.ContentType))
                {
                    if (hasBody)
                    {
                        var message =
                            "Content-Type header is required. Supported types: " + string.Join(", ", allowed);

                        log.Warning(
                            "Request with missing Content-Type header is not allowed. {Message}",
                            message);

                        await krContext.Response.WriteErrorResponseAsync(
                            message: message,
                            statusCode: StatusCodes.Status415UnsupportedMediaType);
                        return;
                    }
                }
                else
                {
                    if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var mediaType))
                    {
                        // Malformed Content-Type → 400 (syntax error, not support issue)
                        var message = $"Invalid Content-Type header value '{context.Request.ContentType}'.";

                        log.Warning(
                            "Malformed Content-Type header '{ContentType}'.",
                            context.Request.ContentType);

                        await krContext.Response.WriteErrorResponseAsync(
                            message: message,
                            statusCode: StatusCodes.Status400BadRequest);
                        return;
                    }

                    var requestContentType = mediaType.MediaType;

                    if (!allowed.Contains(requestContentType, StringComparer.OrdinalIgnoreCase))
                    {
                        var message =
                            $"Request content type '{requestContentType}' is not allowed. Supported types: {string.Join(", ", allowed)}";

                        log.Warning(
                            "Request content type '{ContentType}' is not allowed for this route.",
                            requestContentType);

                        await krContext.Response.WriteErrorResponseAsync(
                            message: message,
                            statusCode: StatusCodes.Status415UnsupportedMediaType);
                        return;
                    }
                }
            }


            if (krContext.HasRequestCulture)
            {
                PowerShellExecutionHelpers.AddCulturePrelude(ps, krContext.Culture, log);
            }
            PowerShellExecutionHelpers.AddScript(ps, code);

            // Extract and add parameters for injection
            ParameterForInjectionInfo.InjectParameters(krContext, ps);

            // Execute the script
            if (isLogVerbose)
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
                if (isLogVerbose)
                {
                    log.Verbose("HttpResponse has already started; skipping KestrunResponse.ApplyTo().");
                }
                return;
            }
            if (isLogVerbose)
            {
                log.Verbose("No redirect detected; applying response to HttpResponse...");
            }
        }
        // optional: catch client cancellation to avoid noisy logs
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // client disconnected – nothing to send
        }
        catch (ParameterForInjectionException pfiiex)
        {
            // Log parameter resolution errors with preview of code
            //   log.Error("Parameter resolution error ({Message}) - {Preview}",
            // pfiiex.Message, code[..Math.Min(40, code.Length)]);
            if (krContext is not null)
            {
                // Return 400 Bad Request for parameter resolution errors
                await krContext.Response.WriteErrorResponseAsync("Invalid request parameters: " + pfiiex.Message, pfiiex.StatusCode);
            }
            else
            {
                throw;
            }
        }
        catch (ParameterBindingException pbaex)
        {
            var fqid = pbaex.ErrorRecord?.FullyQualifiedErrorId;
            var cat = pbaex.ErrorRecord?.CategoryInfo?.Category;
            if (krContext is not null)
            {
                // Return 400 Bad Request for parameter binding errors
                await krContext.Response.WriteErrorResponseAsync("Invalid request parameters: " + pbaex.Message, StatusCodes.Status400BadRequest);
            }
            else
            {
                throw;
            }
        }
        catch (Forms.KrFormException kfex)
        {
            // Log form parsing errors with preview of code
            log.Error("Form parsing error ({Message}) - {Preview}",
                kfex.Message, code[..Math.Min(40, code.Length)]);
            if (krContext is not null)
            {
                // Return 400 Bad Request for form parsing errors
                await krContext.Response.WriteErrorResponseAsync("Invalid form data: " + kfex.Message, kfex.StatusCode);
            }
            else { throw; }
        }
        catch (Exception ex)
        {
            // If we have exception options, set a 500 status code and generic message.
            // Otherwise rethrow to let higher-level middleware handle it (e.g., Developer Exception Page
            if (krContext?.Host?.ExceptionOptions is null)
            { // Log and handle script errors
                log.Error(ex, "PowerShell script failed - {Preview}", code[..Math.Min(40, code.Length)]);
                if (krContext is not null)
                {
                    await krContext.Response.WriteErrorResponseAsync("An internal server error occurred.", StatusCodes.Status500InternalServerError);
                }
                else
                {
                    context.Response.StatusCode = 500; // Internal Server Error
                    context.Response.ContentType = "text/plain; charset=utf-8";
                    await context.Response.WriteAsync("An error occurred while processing your request.");
                }
            }
            else
            {
                // re-throw to let higher-level middleware handle it (e.g., Developer Exception Page)
                throw;
            }
        }
        finally
        {
            if (krContext is not null)
            {
                await ApplyResponseAsync(context, krContext).ConfigureAwait(false);
            }
            // Do not call Response.CompleteAsync here; leaving the response open allows
            // downstream middleware like StatusCodePages to generate a body for status-only responses.
        }
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
