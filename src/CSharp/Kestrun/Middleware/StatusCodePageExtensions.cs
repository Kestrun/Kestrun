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
            // Normalize query format: ASP.NET Core expects leading '?' for non-empty query
            var query = options.QueryFormat;
            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.Trim();
                if (!query.StartsWith("?", StringComparison.Ordinal))
                {
                    query = "?" + query;
                }
            }
            return app.UseStatusCodePagesWithReExecute(options.PathFormat, query);
        }

        if (!string.IsNullOrWhiteSpace(options.ContentType) && !string.IsNullOrWhiteSpace(options.BodyFormat))
        {
            // If both ContentType and BodyFormat are set, use them.
            // Escape curly braces for String.Format safety, but preserve the {0} placeholder used for status code.
            // If the template already contains escaped braces ({{ or }}), assume it is pre-escaped and skip.
            static string EscapeTemplate(string bodyFormat)
            {
                if (bodyFormat.Contains("{{", StringComparison.Ordinal) || bodyFormat.Contains("}}", StringComparison.Ordinal))
                {
                    return bodyFormat; // already escaped
                }

                var escaped = bodyFormat.Replace("{", "{{").Replace("}", "}}");
                // restore the status-code placeholder
                escaped = escaped.Replace("{{0}}", "{0}");
                return escaped;
            }

            var safeBody = EscapeTemplate(options.BodyFormat);
            return app.UseStatusCodePages(options.ContentType, safeBody);
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
