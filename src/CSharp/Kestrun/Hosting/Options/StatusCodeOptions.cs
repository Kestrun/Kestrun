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

}
