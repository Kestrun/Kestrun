using Microsoft.AspNetCore.Diagnostics;
using System.Management.Automation;
using Kestrun.Languages;
using Kestrun.Models;

namespace Kestrun.Hosting;

/// <summary>
/// Extension methods for adding status code pages to a KestrunHost.
/// </summary>
public static class KestrunHostStatusCodePagesExtensions
{
    /// <summary>
    /// Adds a default status code page to the application.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost"/> instance to configure.</param>
    /// <returns>The configured <see cref="KestrunHost"/> instance.</returns>
    public static KestrunHost UseStatusCodePages(this KestrunHost host)
    {

        return host.Use(app => app.UseStatusCodePages());
    }

    /// <summary>
    /// Adds a StatusCodePages middleware with the given options that checks for responses with status codes
    /// between 400 and 599 that do not have a body.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost"/> instance to configure.</param>
    /// <param name="options">The options to configure the status code pages middleware.</param>
    /// <returns>The configured <see cref="KestrunHost"/> instance.</returns>
    public static KestrunHost UseStatusCodePages(this KestrunHost host, StatusCodePagesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return host.Use(app => app.UseStatusCodePages(options));
    }

    /// <summary>
    /// Adds a StatusCodePages middleware with the specified handler that checks for responses with status codes
    /// between 400 and 599 that do not have a body.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost"/> instance to configure.</param>
    /// <param name="handler">The handler to invoke for status code responses.</param>
    /// <returns>The configured <see cref="KestrunHost"/> instance.</returns>
    public static KestrunHost UseStatusCodePages(this KestrunHost host, Func<StatusCodeContext, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return host.Use(app => app.UseStatusCodePages(handler));
    }

    /// <summary>
    /// Adds a StatusCodePages middleware with the specified script options that checks for responses with status codes
    /// between 400 and 599 that do not have a body.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost"/> instance to configure.</param>
    /// <param name="options">The script options to configure the status code pages middleware.</param>
    /// <returns>The configured <see cref="KestrunHost"/> instance.</returns>
    public static KestrunHost UseStatusCodePages(this KestrunHost host, Options.LanguageOptions options)
    {
        var compiled = KestrunHostMapExtensions.CompileScript(options, host.HostLogger);

        async Task Handler(StatusCodeContext context)
        {
            var httpContext = context.HttpContext;

            // If we're running a PowerShell script but the runspace middleware did not execute
            // (e.g., no matched endpoint so UseWhen predicate failed), bootstrap a temporary
            // runspace and KestrunContext so the compiled delegate can run safely.
            if (options.Language == Scripting.ScriptLanguage.PowerShell &&
                !httpContext.Items.ContainsKey(PowerShellDelegateBuilder.PS_INSTANCE_KEY))
            {
                var pool = host.RunspacePool; // throws if not initialized
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

        return host.Use(app => app.UseStatusCodePages(Handler));
    }

    /// <summary>
    /// Adds a StatusCodePages middleware with the specified response body to send. This may include a '{0}' placeholder for the status code.
    /// The middleware checks for responses with status codes between 400 and 599 that do not have a body.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost"/> instance to configure.</param>
    /// <param name="contentType">The content type of the response body.</param>
    /// <param name="bodyFormat">The format string for the response body.</param>
    /// <returns>The configured <see cref="KestrunHost"/> instance.</returns>
    public static KestrunHost UseStatusCodePages(this KestrunHost host, string contentType, string bodyFormat)
    {
        return string.IsNullOrWhiteSpace(contentType)
            ? throw new ArgumentException("contentType is required.", nameof(contentType))
            : string.IsNullOrWhiteSpace(bodyFormat)
            ? throw new ArgumentException("bodyFormat is required.", nameof(bodyFormat))
            : host.Use(app => app.UseStatusCodePages(contentType, bodyFormat));
    }


    /// <summary>
    /// Adds a StatusCodePages middleware to the pipeline. Specifies that responses should be handled by redirecting
    /// with the given location URL template. This may include a '{0}' placeholder for the status code. URLs starting
    /// with '~' will have PathBase prepended, where any other URL will be used as is.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost"/> instance to configure.</param>
    /// <param name="locationFormat">The location format string to redirect to. This can include placeholders for the status code.</param>
    /// <returns>The configured <see cref="KestrunHost"/> instance.</returns>
    public static KestrunHost UseStatusCodePagesWithRedirects(this KestrunHost host, string locationFormat)
    {
        return string.IsNullOrWhiteSpace(locationFormat)
            ? throw new ArgumentException("locationFormat is required.", nameof(locationFormat))
            : host.Use(app => app.UseStatusCodePagesWithRedirects(locationFormat));
    }

    /// <summary>
    /// Adds a StatusCodePages middleware to the pipeline. Specifies that the response body should be generated by
    /// re-executing the request pipeline using an alternate path. This path may contain a '{0}' placeholder of the status code.
    /// </summary>
    /// <param name="host">The <see cref="KestrunHost"/> instance to configure.</param>
    /// <param name="pathFormat">The path format string to use for re-executing the request pipeline.</param>
    /// <param name="queryFormat">The query format string to use for re-executing the request pipeline.</param>
    /// <returns>The configured <see cref="KestrunHost"/> instance.</returns>
    public static KestrunHost UseStatusCodePagesWithReExecute(
        this KestrunHost host,
        string pathFormat,
        string? queryFormat = null)
    {
        return string.IsNullOrWhiteSpace(pathFormat)
            ? throw new ArgumentException("pathFormat is required.", nameof(pathFormat))
            : host.Use(app => app.UseStatusCodePagesWithReExecute(pathFormat, queryFormat));
    }
}
