using System.Management.Automation;
using Kestrun.Languages;
using Kestrun.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace Kestrun.Hosting.Options;
/// <summary>
/// Options for configuring Kestrun-style exception handling middleware.
/// </summary>
public sealed class ExceptionOptions : Microsoft.AspNetCore.Builder.ExceptionHandlerOptions
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
                var compiled = KestrunHostMapExtensions.CompileScript(value, Host.HostLogger);
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



    private static RequestDelegate BuildScriptExceptionHandler(ExceptionOptions o, RequestDelegate compiled)
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
            var kr = new KestrunContext(req, res, httpContext);

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
    /// <returns></returns>
    public void UseJsonExceptionHandler(bool useProblemDetails, bool includeDetailsInDevelopment)
    {
        ExceptionHandler = async context =>
        {
            var env = context.RequestServices.GetService(typeof(IHostEnvironment)) as IHostEnvironment;
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var ex = feature?.Error;

#if NET9_0_OR_GREATER
            var status = StatusCodeSelector?.Invoke(ex ?? new Exception("Unhandled exception"))
                         ?? StatusCodes.Status500InternalServerError;
#else
            var status = LegacyStatusCodeSelector?.Invoke(ex ?? new Exception("Unhandled exception"))
                         ?? StatusCodes.Status500InternalServerError;
#endif
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";

            if (useProblemDetails)
            {
                // RFC 7807-esque payload
                var body = new
                {
                    type = "about:blank",
                    title = status switch
                    {
                        400 => "Bad Request",
                        401 => "Unauthorized",
                        403 => "Forbidden",
                        404 => "Not Found",
                        _ => "Internal Server Error"
                    },
                    status,
                    detail = (includeDetailsInDevelopment && env?.IsDevelopment() == true)
                                ? ex?.ToString()
                                : ex?.Message
                };

                await context.Response.WriteAsJsonAsync(body);
                return;
            }

            // Minimal JSON
            await context.Response.WriteAsJsonAsync(new
            {
                error = true,
                status,
                message = (includeDetailsInDevelopment && env?.IsDevelopment() == true)
                            ? ex?.ToString()
                            : ex?.Message
            });
        };
    }

}
