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
        var compiled = options.Host.CompileScript(options.LanguageOptions);
        var handler = BuildScriptHandler(options, compiled);
        return app.UseStatusCodePages(handler);
    }
    private static bool HasValue(string? s) => !string.IsNullOrWhiteSpace(s);

    /// <summary>
    /// Normalizes the query string to ensure it starts with '?' if not empty.
    /// </summary>
    /// <param name="query">The query string.</param>
    /// <returns>The normalized query string.</returns>
    private static string NormalizeQuery(string? query)
    {
        if (query is null)
        {
            return string.Empty;
        }

        var q = query.Trim();
        return q.Length > 0 && q[0] == '?'
            ? q
            : "?" + q;
    }

    /// <summary>
    /// Escape curly braces for String.Format safety, but preserve the {0} placeholder used for status code.
    /// </summary>
    /// <param name="bodyFormat">The body format string to escape.</param>
    /// <returns>The escaped body format string.</returns>
    /// <remarks>If the template already contains escaped braces ({{ or }}), assume it is pre-escaped and skip.</remarks>
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

    /// <summary>
    /// Builds a status code handler that executes the compiled script delegate.
    /// </summary>
    /// <param name="options">The status code options.</param>
    /// <param name="compiled">The compiled request delegate.</param>
    /// <returns>A function that handles the status code context.</returns>
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
            var kr = new KestrunContext(pool.Host, httpContext);
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
