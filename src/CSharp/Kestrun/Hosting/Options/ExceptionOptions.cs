using System.Management.Automation;
using Kestrun.Languages;
using Kestrun.Models;
using Kestrun.Runtime;
using Microsoft.AspNetCore.Diagnostics;

namespace Kestrun.Hosting.Options;
/// <summary>
/// Options for configuring Kestrun-style exception handling middleware.
/// </summary>
public sealed class ExceptionOptions : ExceptionHandlerOptions
{
    /// <summary>
    ///
    /// </summary>
    private LanguageOptions? _languageOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionOptions"/> class.
    /// </summary>
    /// <param name="host">The KestrunHost instance associated with these options.</param>
    public ExceptionOptions(KestrunHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
    }

    /// <summary>
    /// Optional scripting options (e.g., PowerShell). If present, a script-based handler is used.
    /// </summary>
    public LanguageOptions? LanguageOptions
    {
        get => _languageOptions;
        set
        {
            _languageOptions = value;
            if (value is not null && ExceptionHandler is null)
            {
                var compiled = Host.CompileScript(value, Host.Logger);
                ExceptionHandler = BuildScriptExceptionHandler(this, compiled);
            }
        }
    }

    /// <summary>Host is needed for runspace/bootstrap if using PowerShell.</summary>
    public required KestrunHost Host { get; init; }

    // .NET 8 compatibility: ExceptionHandlerOptions in .NET 8 does not expose StatusCodeSelector.
    // Provide our own selector for net8.0; on net9.0+ prefer the base property.
#if !NET9_0_OR_GREATER
    /// <summary>
    /// .NET 8: Optional custom status code selector for JSON/script fallback.
    /// Lets you map exception types to status codes.
    /// </summary>
    public Func<Exception, int>? LegacyStatusCodeSelector { get; set; }
#endif



    private RequestDelegate BuildScriptExceptionHandler(ExceptionOptions o, RequestDelegate compiled)
    {
        return async httpContext =>
        {
            // Ensure a PS runspace exists if we're executing a PowerShell-based handler outside the normal PS middleware path
            var needsBootstrap = o.LanguageOptions!.Language == Scripting.ScriptLanguage.PowerShell &&
                                 !httpContext.Items.ContainsKey(PowerShellDelegateBuilder.PS_INSTANCE_KEY);

            if (!needsBootstrap)
            {
                await compiled(httpContext);
                return;
            }

            var pool = o.Host.RunspacePool; // throws if not initialized
            var runspace = await pool.AcquireAsync(httpContext.RequestAborted);
            using var ps = PowerShell.Create();
            ps.Runspace = runspace;

            // Build Kestrun abstractions and inject for the script to consume
            var req = await KestrunRequest.NewRequest(httpContext);
            var res = new KestrunResponse(req) { StatusCode = httpContext.Response.StatusCode };
            var kr = new KestrunContext(Host, req, res, httpContext);

            httpContext.Items[PowerShellDelegateBuilder.PS_INSTANCE_KEY] = ps;
            httpContext.Items[PowerShellDelegateBuilder.KR_CONTEXT_KEY] = kr;
            ps.Runspace.SessionStateProxy.SetVariable("Context", kr);

            try
            {
                await compiled(httpContext);
            }
            finally
            {
                o.Host.RunspacePool.Release(ps.Runspace);
                ps.Dispose();
                _ = httpContext.Items.Remove(PowerShellDelegateBuilder.PS_INSTANCE_KEY);
                _ = httpContext.Items.Remove(PowerShellDelegateBuilder.KR_CONTEXT_KEY);
            }
        };
    }

    /// <summary>
    /// Builds a JSON fallback handler that can be used if no script/inline handler is provided.
    /// </summary>
    /// <param name="useProblemDetails">Whether to use RFC 7807 ProblemDetails in the JSON fallback.</param>
    /// <param name="includeDetailsInDevelopment">Whether to include exception details in development.</param>
    /// <param name="compress">Whether to compress the JSON response.</param>
    /// <returns></returns>
    public void UseJsonExceptionHandler(bool useProblemDetails, bool includeDetailsInDevelopment, bool compress = false)
    {
        ExceptionHandler = async httpContext =>
        {
            // If the response already started, let the server bubble up
            if (httpContext.Response.HasStarted)
            {
                // optional: log here
                return;
            }

            var env = httpContext.RequestServices.GetService(typeof(IHostEnvironment)) as IHostEnvironment;
            var feature = httpContext.Features.Get<IExceptionHandlerFeature>();
            var ex = feature?.Error;

#if NET9_0_OR_GREATER
            var status = StatusCodeSelector?.Invoke(ex ?? new Exception("Unhandled exception"))
                         ?? StatusCodes.Status500InternalServerError;
#else
            var status = LegacyStatusCodeSelector?.Invoke(ex ?? new Exception("Unhandled exception"))
                         ?? StatusCodes.Status500InternalServerError;
#endif

            var kestrunContext = new KestrunContext(Host, httpContext);

            // Always set the HTTP status first
            kestrunContext.Response.StatusCode = status;

            // Choose the right media type
            var contentType = useProblemDetails
                ? "application/problem+json; charset=utf-8"
                : "application/json; charset=utf-8";

            // No-cache for error payloads
            httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            httpContext.Response.Headers.Pragma = "no-cache";
            httpContext.Response.Headers.Expires = "0";

            // Build body
            var body = new Dictionary<string, object?>
            {
                ["status"] = status,
                ["traceId"] = System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier,
                ["instance"] = httpContext.Request.Path.HasValue ? httpContext.Request.Path.Value : "/"
            };

            if (useProblemDetails)
            {
                body["type"] = "about:blank"; // you can swap for a doc URL later
                body["title"] = status switch
                {
                    400 => "Bad Request",
                    401 => "Unauthorized",
                    403 => "Forbidden",
                    404 => "Not Found",
                    _ => "Internal Server Error"
                };
                body["detail"] = includeDetailsInDevelopment && EnvironmentHelper.IsDevelopment()
                                    ? ex?.ToString()
                                    : ex?.Message;
            }
            else
            {
                body["error"] = true;
                body["message"] = (includeDetailsInDevelopment && EnvironmentHelper.IsDevelopment())
                                    ? ex?.ToString()
                                    : ex?.Message;
            }

            // Note: `compress` means "compact JSON". Pretty-print when not compressing.
            // Adjust the `5` (maxDepth) if your WriteJsonResponseAsync uses it that way.
            await kestrunContext.Response.WriteJsonResponseAsync(
                body,
                5,
                compress,       // compact when true
                status,
                contentType
            );

            await kestrunContext.Response.ApplyTo(httpContext.Response);
        };
    }


}
