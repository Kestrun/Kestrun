namespace Kestrun.Hosting.Options;
/// <summary>
/// Options for configuring Kestrun-style exception handling middleware.
/// </summary>
public sealed class ExceptionOptions
{
    /// <summary>Inline handler to run inside the exception pipeline (wins over path).</summary>
    public RequestDelegate? Handler { get; init; }

    /// <summary>Re-execute path (e.g. "/error") if no inline handler/script is supplied.</summary>
    public PathString? ReExecutePath { get; init; }

    /// <summary>Optional scripting options (e.g., PowerShell). If present, a script-based handler is used.</summary>
    public LanguageOptions? LanguageOptions { get; init; }

    /// <summary>Host is needed for runspace/bootstrap if using PowerShell.</summary>
    public required KestrunHost Host { get; init; }

    /// <summary>Include exception details in development for JSON fallback.</summary>
    public bool IncludeDetailsInDevelopment { get; init; } = true;

    /// <summary>Emit RFC 7807 ProblemDetails in the JSON fallback.</summary>
    public bool UseProblemDetails { get; init; } = true;

    /// <summary>
    /// Optional custom status code selector when using the JSON/script fallback
    /// (lets you map exception types to status codes).
    /// </summary>
    public Func<HttpContext, Exception?, int>? StatusCodeSelector { get; init; }
}
