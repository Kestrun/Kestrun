using System.Management.Automation;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Languages;
using Kestrun.Models;
using Microsoft.AspNetCore.Diagnostics;
namespace Kestrun.Middleware;

/// <summary>
/// Extension methods for adding status code pages middleware.
/// </summary>
public static class StatusCodePageExtensions
{

    /// <summary>
    /// Applies the configured status code pages middleware to the specified application builder.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="options">The status code options.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseStatusCodePages(this IApplicationBuilder app, StatusCodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // If Options is set, use it directly
        if (options.Options != null)
        {
            return app.UseStatusCodePages(options.Options);
        }

        // If LocationFormat is set, use redirects
        if (!string.IsNullOrWhiteSpace(options.LocationFormat))
        {
            return app.UseStatusCodePagesWithRedirects(options.LocationFormat);
        }

        // If PathFormat is set, use re-execute
        if (!string.IsNullOrWhiteSpace(options.PathFormat))
        {
            return app.UseStatusCodePagesWithReExecute(options.PathFormat, options.QueryFormat);
        }

        if (!string.IsNullOrWhiteSpace(options.ContentType) && !string.IsNullOrWhiteSpace(options.BodyFormat))
        {
            // If both ContentType and BodyFormat are set, use them
            return app.UseStatusCodePages(options.ContentType, options.BodyFormat);
        }

        // If ScriptOptions is set, use a custom handler to execute the script
        if (options.LanguageOptions != null)
        {
            var compiled = KestrunHostMapExtensions.CompileScript(options.LanguageOptions, options.Host.HostLogger);

            async Task Handler(StatusCodeContext context)
            {
                var httpContext = context.HttpContext;

                // If we're running a PowerShell script but the runspace middleware did not execute
                // (e.g., no matched endpoint so UseWhen predicate failed), bootstrap a temporary
                // runspace and KestrunContext so the compiled delegate can run safely.
                if (options.LanguageOptions.Language == Scripting.ScriptLanguage.PowerShell &&
                    !httpContext.Items.ContainsKey(PowerShellDelegateBuilder.PS_INSTANCE_KEY))
                {
                    var pool = options.Host.RunspacePool; // throws if not initialized
                    var runspace = await pool.AcquireAsync(httpContext.RequestAborted);
                    using var ps = PowerShell.Create();
                    ps.Runspace = runspace;

                    // Build Kestrun abstractions and inject into context for PS delegate to use
                    var req = await KestrunRequest.NewRequest(httpContext);
                    var res = new KestrunResponse(req)
                    {
                        StatusCode = httpContext.Response.StatusCode
                    };
                    var kr = new KestrunContext(req, res, httpContext);

                    httpContext.Items[PowerShellDelegateBuilder.PS_INSTANCE_KEY] = ps;
                    httpContext.Items[PowerShellDelegateBuilder.KR_CONTEXT_KEY] = kr;
                    var ss = ps.Runspace.SessionStateProxy;
                    ss.SetVariable("Context", kr);

                    try
                    {
                        await compiled(httpContext);
                    }
                    finally
                    {
                        pool.Release(ps.Runspace);
                        ps.Dispose();
                        _ = httpContext.Items.Remove(PowerShellDelegateBuilder.PS_INSTANCE_KEY);
                        _ = httpContext.Items.Remove(PowerShellDelegateBuilder.KR_CONTEXT_KEY);
                    }
                }
                else
                {
                    await compiled(httpContext);
                }
            }
            return app.UseStatusCodePages(Handler);
        }

        return app.UseStatusCodePages(); // Default to built-in behavior
    }
}
