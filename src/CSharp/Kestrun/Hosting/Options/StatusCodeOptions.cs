using System.Management.Automation;
using Kestrun.Languages;
using Kestrun.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace Kestrun.Hosting.Options;

/// <summary>
/// Options for configuring status code pages middleware.
/// </summary>
public class StatusCodeOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StatusCodeOptions"/> class.
    /// </summary>
    /// <param name="host">The KestrunHost instance associated with these options.</param>
    public StatusCodeOptions(KestrunHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
    }
    /// <summary>
    /// Gets the KestrunHost instance associated with these options.
    /// </summary>
    public KestrunHost Host { get; }
    /// <summary>
    /// Gets or sets the content type for the status code response. Default is "text/plain".
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets or sets the body format string for the status code response. Default is "Status Code: {0}".
    /// The format string should contain a single placeholder for the status code.
    /// </summary>
    public string? BodyFormat { get; init; }

    /// <summary>
    /// Gets or sets the script options to execute for generating the status code response.
    /// If set, this script will be executed instead of using the BodyFormat.
    /// </summary>
    public LanguageOptions? LanguageOptions { get; init; }


    /// <summary>
    /// Gets or sets the underlying StatusCodePagesOptions to configure the status code pages middleware.
    /// </summary>
    public StatusCodePagesOptions? Options { get; init; }

    /// <summary>
    /// Gets or sets the location format string for the status code response. If set, the middleware will issue a redirect to the specified location.
    /// The format string should contain a single placeholder for the status code.
    /// If both LocationFormat and PathFormat are set, LocationFormat takes precedence.
    /// If neither is set, no redirect will be issued.
    /// </summary>
    public string? LocationFormat { get; init; }

    /// <summary>
    /// Gets or sets the path format string for the status code response. If set, the middleware will re-execute the request pipeline for the specified path.
    /// The format string should contain a single placeholder for the status code.
    /// If both LocationFormat and PathFormat are set, LocationFormat takes precedence.
    /// If neither is set, no re-execution will be performed.
    /// </summary>
    public string? PathFormat { get; init; }

    /// <summary>
    /// Gets or sets the query format string for the status code response. If set, the middleware will append the specified query string to the path when re-executing the request pipeline.
    /// The format string should contain a single placeholder for the status code.
    /// This is only used if PathFormat is also set.
    /// </summary>
    public string? QueryFormat { get; init; }

    /// <summary>
    /// Applies the configured status code pages middleware to the specified application builder.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <exception cref="InvalidOperationException">Thrown when no valid status code page configuration is found.</exception>
    public void Apply(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // If Options is set, use it directly
        if (Options != null)
        {
            _ = app.UseStatusCodePages(Options);
            return;
        }

        // If LocationFormat is set, use redirects
        if (!string.IsNullOrWhiteSpace(LocationFormat))
        {
            _ = app.UseStatusCodePagesWithRedirects(LocationFormat);
            return;
        }

        // If PathFormat is set, use re-execute
        if (!string.IsNullOrWhiteSpace(PathFormat))
        {
            _ = app.UseStatusCodePagesWithReExecute(PathFormat, QueryFormat);
            return;
        }

        if (!string.IsNullOrWhiteSpace(ContentType) && !string.IsNullOrWhiteSpace(BodyFormat))
        {
            // If both ContentType and BodyFormat are set, use them
            _ = app.UseStatusCodePages(ContentType, BodyFormat);
            return;
        }

        // If ScriptOptions is set, use a custom handler to execute the script
        if (LanguageOptions != null)
        {
            var compiled = KestrunHostMapExtensions.CompileScript(LanguageOptions, Host.HostLogger);

            async Task Handler(StatusCodeContext context)
            {
                var httpContext = context.HttpContext;

                // If we're running a PowerShell script but the runspace middleware did not execute
                // (e.g., no matched endpoint so UseWhen predicate failed), bootstrap a temporary
                // runspace and KestrunContext so the compiled delegate can run safely.
                if (LanguageOptions.Language == Scripting.ScriptLanguage.PowerShell &&
                    !httpContext.Items.ContainsKey(PowerShellDelegateBuilder.PS_INSTANCE_KEY))
                {
                    var pool = Host.RunspacePool; // throws if not initialized
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
            _ = app.UseStatusCodePages(Handler);
            return;
        }

        throw new InvalidOperationException("No valid status code page configuration found. Please set one of the following properties: Options, LocationFormat, PathFormat, (ContentType and BodyFormat), or ScriptOptions.");
    }
}
