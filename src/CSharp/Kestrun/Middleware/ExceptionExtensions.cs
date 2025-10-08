using System.Management.Automation;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Languages;
using Kestrun.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace Kestrun.Middleware;

/// <summary>
/// Extension methods for adding Kestrun-style exception handling middleware.
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// Applies exception handling to the app using Kestrun-style options:
    /// 1) Inline handler (RequestDelegate)
    /// 2) Script handler (LanguageOptions)
    /// 3) Re-execute path
    /// 4) Safe JSON/ProblemDetails fallback
    /// </summary>
    public static IApplicationBuilder UseException(this IApplicationBuilder app, ExceptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // 1) direct inline handler
        if (TryUseInlineHandler(app, options) is { } inlineApplied)
        {
            return inlineApplied;
        }

        // 2) script handler (e.g., PowerShell)
        if (TryUseScriptHandler(app, options) is { } scriptApplied)
        {
            return scriptApplied;
        }

        // 3) re-execute to a path
        if (TryUseReExecute(app, options) is { } reexecApplied)
        {
            return reexecApplied;
        }

        // 4) safe JSON/ProblemDetails fallback
        return UseJsonFallback(app, options);
    }

    private static IApplicationBuilder? TryUseInlineHandler(IApplicationBuilder app, ExceptionOptions o)
        => o.Handler is null ? null : app.UseExceptionHandler(builder => builder.Run(o.Handler!));

    private static IApplicationBuilder? TryUseReExecute(IApplicationBuilder app, ExceptionOptions o)
        => o.ReExecutePath?.Value is null ? null : app.UseExceptionHandler(o.ReExecutePath.Value.Value);

    private static IApplicationBuilder? TryUseScriptHandler(IApplicationBuilder app, ExceptionOptions o)
    {
        if (o.LanguageOptions is null)
        {
            return null;
        }

        var compiled = KestrunHostMapExtensions.CompileScript(o.LanguageOptions, o.Host.HostLogger);
        var handler = BuildScriptExceptionHandler(o, compiled);
        return app.UseExceptionHandler(b => b.Run(handler));
    }

    private static IApplicationBuilder UseJsonFallback(IApplicationBuilder app, ExceptionOptions o)
        => app.UseExceptionHandler(builder => builder.Run(BuildJsonFallback(o)));

    // --- Builders ---

    private static RequestDelegate BuildJsonFallback(ExceptionOptions o)
    {
        return async context =>
        {
            var env = context.RequestServices.GetService(typeof(IHostEnvironment)) as IHostEnvironment;
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var ex = feature?.Error;

            var status = o.StatusCodeSelector?.Invoke(context, ex) ?? StatusCodes.Status500InternalServerError;
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";

            if (o.UseProblemDetails)
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
                    detail = (o.IncludeDetailsInDevelopment && env?.IsDevelopment() == true)
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
                message = (o.IncludeDetailsInDevelopment && env?.IsDevelopment() == true)
                            ? ex?.ToString()
                            : ex?.Message
            });
        };
    }

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
}
