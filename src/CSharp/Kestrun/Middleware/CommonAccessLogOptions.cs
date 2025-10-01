using Serilog.Events;

namespace Kestrun.Middleware;

/// <summary>
/// Options controlling the behaviour of the <see cref="CommonAccessLogMiddleware"/>.
/// </summary>
public sealed class CommonAccessLogOptions
{
    /// <summary>
    /// The default timestamp format used by Apache HTTPD common/combined logs.
    /// </summary>
    public const string DefaultTimestampFormat = "dd/MMM/yyyy:HH:mm:ss zzz";

    /// <summary>
    /// Gets or sets the log level used when writing access log entries.
    /// </summary>
    public LogEventLevel Level { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Gets or sets a value indicating whether the request query string should be included in the request line.
    /// </summary>
    public bool IncludeQueryString { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the request protocol should be included in the request line.
    /// </summary>
    public bool IncludeProtocol { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the elapsed request time in milliseconds should be appended to the log entry.
    /// </summary>
    public bool IncludeElapsedMilliseconds { get; set; }
        = false;

    /// <summary>
    /// Gets or sets a value indicating whether the timestamp should be written using UTC time rather than the server local time.
    /// </summary>
    public bool UseUtcTimestamp { get; set; }
        = false;

    /// <summary>
    /// Gets or sets the timestamp format used when rendering the access log entry.
    /// </summary>
    public string TimestampFormat { get; set; } = DefaultTimestampFormat;

    /// <summary>
    /// Gets or sets the name of the HTTP header that should be consulted for the client address
    /// (for example <c>X-Forwarded-For</c>). When the header is missing the connection remote address is used.
    /// </summary>
    public string? ClientAddressHeader { get; set; }

    /// <summary>
    /// Gets or sets the time provider used when rendering timestamps.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// Gets or sets the Serilog logger used to emit access log entries. When not specified the
    /// middleware will use the application logger registered in dependency injection.
    /// </summary>
    public Serilog.ILogger? Logger { get; set; }
}
