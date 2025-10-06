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
        // 1) direct delegate options
        if (TryUseDirectOptions(app, options) is { } direct)
        {
            return direct;
        }

        // 2) redirects
        if (TryUseRedirects(app, options) is { } redirects)
        {
            return redirects;
        }

        // 3) re-execute
        if (TryUseReExecute(app, options) is { } reexec)
        {
            return reexec;
        }

        // 4) static body
        if (TryUseStaticBody(app, options) is { } staticBody)
        {
            return staticBody;
        }

        // 5) custom script handler
        if (TryUseScriptHandler(app, options) is { } script)
        {
            return script;
        }

        // default to built-in behavior
        return app.UseStatusCodePages();
    }

    private static IApplicationBuilder? TryUseDirectOptions(IApplicationBuilder app, StatusCodeOptions options)
        => options.Options is not null ? app.UseStatusCodePages(options.Options) : null;

    private static IApplicationBuilder? TryUseRedirects(IApplicationBuilder app, StatusCodeOptions options)
        => HasValue(options.LocationFormat) ? app.UseStatusCodePagesWithRedirects(options.LocationFormat!) : null;

    private static IApplicationBuilder? TryUseReExecute(IApplicationBuilder app, StatusCodeOptions options)
    {
        if (!HasValue(options.PathFormat))
        {
            return null;
        }
        var query = NormalizeQuery(options.QueryFormat);
        return app.UseStatusCodePagesWithReExecute(options.PathFormat!, query);
    }

    private static IApplicationBuilder? TryUseStaticBody(IApplicationBuilder app, StatusCodeOptions options)
    {
        if (!HasValue(options.ContentType) || !HasValue(options.BodyFormat))
        {
            return null;
        }
        var safeBody = EscapeTemplate(options.BodyFormat!);
        return app.UseStatusCodePages(options.ContentType!, safeBody);
    }

    private static IApplicationBuilder? TryUseScriptHandler(IApplicationBuilder app, StatusCodeOptions options)
    {
        if (options.LanguageOptions is null)
        {
            return null;
        }
        var compiled = KestrunHostMapExtensions.CompileScript(options.LanguageOptions, options.Host.HostLogger);
        var handler = BuildScriptHandler(options, compiled);
        return app.UseStatusCodePages(handler);
    }

    private static bool HasValue(string? s) => !string.IsNullOrWhiteSpace(s);

    private static string? NormalizeQuery(string? query)
    {
        if (!HasValue(query))
        {
            return query;
        }
        var q = query!.Trim();
        return q.StartsWith('?') ? q : "?" + q;
    }

    // Escape curly braces for String.Format safety, but preserve the {0} placeholder used for status code.
    // If the template already contains escaped braces ({{ or }}), assume it is pre-escaped and skip.
    private static string EscapeTemplate(string bodyFormat)
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

    private static Func<StatusCodeContext, Task> BuildScriptHandler(StatusCodeOptions options, RequestDelegate compiled)
    {
        return async context =>
        {
            var httpContext = context.HttpContext;

            // If running a PowerShell script but the runspace middleware did not execute
            // (e.g., no matched endpoint so UseWhen predicate failed), bootstrap a temporary
            // runspace and KestrunContext so the compiled delegate can run safely.
            var needsBootstrap = options.LanguageOptions!.Language == Scripting.ScriptLanguage.PowerShell &&
                                 !httpContext.Items.ContainsKey(PowerShellDelegateBuilder.PS_INSTANCE_KEY);

            if (!needsBootstrap)
            {
                await compiled(httpContext);
                return;
            }

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
        };
    }
}
