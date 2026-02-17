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

                        await WriteErrorResponseWithCustomHandlerAsync(
                            context,
                            krContext,
                            ps,
                            log,
                            message,
                            StatusCodes.Status415UnsupportedMediaType).ConfigureAwait(false);
                        return;
                    }
                }
                else
                {
                    if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var mediaType))
                    {
                        // Malformed Content-Type → 400 (syntax error, not support issue)
                        var message = $"Invalid Content-Type header value '{context.Request.ContentType}'.";

                        log.WarningSanitized(
                            "Malformed Content-Type header '{ContentType}'.",
                            context.Request.ContentType);

                        await WriteErrorResponseWithCustomHandlerAsync(
                            context,
                            krContext,
                            ps,
                            log,
                            message,
                            StatusCodes.Status400BadRequest).ConfigureAwait(false);
                        return;
                    }
                    // Canonicalize the request content type and check against allowed list
                    var requestContentTypeRaw = mediaType.MediaType ?? string.Empty;
                    // Canonicalize the request content type and check against allowed list
                    var requestContentTypeCanonical = MediaTypeHelper.Canonicalize(requestContentTypeRaw);
                    // Check both raw and canonical forms against the allowed list to allow for flexible matching
                    var rawAllowed = allowed.Contains(requestContentTypeRaw, StringComparer.OrdinalIgnoreCase);
                    // Canonicalize the request content type and check against allowed list
                    var canonicalAllowed = allowed
                        .Select(MediaTypeHelper.Canonicalize)
                        .Contains(requestContentTypeCanonical, StringComparer.OrdinalIgnoreCase);
                    // If neither the raw nor canonical content type is allowed, return 415 Unsupported Media Type
                    if (!rawAllowed && !canonicalAllowed)
                    {
                        var message =
                            $"Request content type '{requestContentTypeRaw}' is not allowed. Supported types: {string.Join(", ", allowed)}";

                        log.Warning(
                            "Request content type '{ContentType}' (canonical '{Canonical}') is not allowed for this route.",
                            requestContentTypeRaw,
                            requestContentTypeCanonical);

                        await WriteErrorResponseWithCustomHandlerAsync(
                            context,
                            krContext,
                            ps,
                            log,
                            message,
                            StatusCodes.Status415UnsupportedMediaType).ConfigureAwait(false);
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

            var postponed = krContext.Response.PostPonedWriteObject;
            if (postponed.Error is int postponedError && postponedError != 0)
            {
                log.Error("Postponed response contains error code {ErrorCode}; throwing before response write.", postponedError);
                throw new InvalidOperationException($"Postponed response error detected: {postponedError}");
            }

            if (krContext.Response.HasPostPonedWriteObject)
            {
                if (isLogVerbose)
                {
                    log.Verbose("Postponed Write-KrResponse detected; applying response with Write-KrResponse.");
                }
                await krContext.Response.WriteResponseAsync(postponed).ConfigureAwait(false);
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
                await WriteErrorResponseWithCustomHandlerAsync(
                    context,
                    krContext,
                    ps,
                    log,
                    "Invalid request parameters: " + pfiiex.Message,
                    pfiiex.StatusCode,
                    pfiiex).ConfigureAwait(false);
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
                await WriteErrorResponseWithCustomHandlerAsync(
                    context,
                    krContext,
                    ps,
                    log,
                    "Invalid request parameters: " + pbaex.Message,
                    StatusCodes.Status400BadRequest,
                    pbaex).ConfigureAwait(false);
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
                await WriteErrorResponseWithCustomHandlerAsync(
                    context,
                    krContext,
                    ps,
                    log,
                    "Invalid form data: " + kfex.Message,
                    kfex.StatusCode,
                    kfex).ConfigureAwait(false);
            }
            else { throw; }
        }
        catch (InvalidOperationException ioex) when (ioex.Message.StartsWith("Postponed response error detected:", StringComparison.Ordinal))
        {
            log.Error(ioex, "Postponed response error detected while applying Write-KrResponse result.");
            if (krContext is not null)
            {
                await WriteErrorResponseWithCustomHandlerAsync(
                    context,
                    krContext,
                    ps,
                    log,
                    "An internal server error occurred.",
                    StatusCodes.Status500InternalServerError,
                    ioex).ConfigureAwait(false);
            }
            else
            {
                throw;
            }
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
                    await WriteErrorResponseWithCustomHandlerAsync(
                        context,
                        krContext,
                        ps,
                        log,
                        "An internal server error occurred.",
                        StatusCodes.Status500InternalServerError,
                        ex).ConfigureAwait(false);
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
    /// Writes an error response using a custom PowerShell handler when configured; otherwise falls back to the built-in error response writer.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    /// <param name="krContext">Current Kestrun context.</param>
    /// <param name="ps">PowerShell instance bound to the request runspace.</param>
    /// <param name="log">Logger instance.</param>
    /// <param name="message">Error message to expose to the handler/fallback writer.</param>
    /// <param name="statusCode">HTTP status code for the error.</param>
    /// <param name="exception">Optional exception that triggered the error flow.</param>
    private static async Task WriteErrorResponseWithCustomHandlerAsync(
        HttpContext context,
        KestrunContext krContext,
        PowerShell ps,
        Serilog.ILogger log,
        string message,
        int statusCode,
        Exception? exception = null)
    {
        if (!await TryExecuteCustomErrorResponseScriptAsync(context, krContext, ps, log, message, statusCode, exception).ConfigureAwait(false))
        {
            await krContext.Response.WriteErrorResponseAsync(message, statusCode).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Attempts to execute a configured custom PowerShell error response script for the current request.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    /// <param name="krContext">Current Kestrun context.</param>
    /// <param name="ps">PowerShell instance bound to the request runspace.</param>
    /// <param name="log">Logger instance.</param>
    /// <param name="message">Error message to expose to the script.</param>
    /// <param name="statusCode">HTTP status code to expose to the script.</param>
    /// <param name="exception">Optional exception to expose to the script.</param>
    /// <returns>True when a custom script executed successfully; otherwise false.</returns>
    private static async Task<bool> TryExecuteCustomErrorResponseScriptAsync(
        HttpContext context,
        KestrunContext krContext,
        PowerShell ps,
        Serilog.ILogger log,
        string message,
        int statusCode,
        Exception? exception)
    {
        var script = krContext.Host.PowerShellErrorResponseScript;
        if (string.IsNullOrWhiteSpace(script) || ps.Runspace is null || context.Response.HasStarted)
        {
            return false;
        }

        try
        {
            using var customErrorPs = PowerShell.Create();
            customErrorPs.Runspace = ps.Runspace;

            var sessionState = customErrorPs.Runspace.SessionStateProxy;
            sessionState.SetVariable("Context", krContext);
            sessionState.SetVariable("KrContext", krContext);
            sessionState.SetVariable("StatusCode", statusCode);
            sessionState.SetVariable("ErrorMessage", message);
            sessionState.SetVariable("Exception", exception);

            if (krContext.HasRequestCulture)
            {
                PowerShellExecutionHelpers.AddCulturePrelude(customErrorPs, krContext.Culture, log);
            }

            PowerShellExecutionHelpers.AddScript(customErrorPs, script);
            _ = await customErrorPs.InvokeAsync(log, context.RequestAborted).ConfigureAwait(false);

            if (customErrorPs.Streams.Error.Count > 0)
            {
                log.Warning("Custom PowerShell error response script reported errors; falling back to default error response.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Custom PowerShell error response script failed; falling back to default error response.");
            return false;
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
