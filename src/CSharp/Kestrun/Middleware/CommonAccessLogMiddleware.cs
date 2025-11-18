using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Kestrun.Middleware;

/// <summary>
/// ASP.NET Core middleware that emits Apache style common access log entries using Serilog.
/// </summary>
public sealed class CommonAccessLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<CommonAccessLogOptions> _optionsMonitor;
    private readonly Serilog.ILogger _defaultLogger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommonAccessLogMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="optionsMonitor">The options monitor for <see cref="CommonAccessLogOptions"/>.</param>
    /// <param name="logger">The Serilog logger instance.</param>
    public CommonAccessLogMiddleware(
        RequestDelegate next,
        IOptionsMonitor<CommonAccessLogOptions> optionsMonitor,
        Serilog.ILogger logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        _defaultLogger = CreateScopedLogger(logger);
    }

    /// <summary>
    /// Invokes the middleware for the specified HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            WriteAccessLog(context, stopwatch.Elapsed);
        }
    }

    private void WriteAccessLog(HttpContext context, TimeSpan elapsed)
    {
        CommonAccessLogOptions options;
        try
        {
            options = _optionsMonitor.CurrentValue ?? new CommonAccessLogOptions();
        }
        catch
        {
            options = new CommonAccessLogOptions();
        }

        var logger = ResolveLogger(options);
        if (!logger.IsEnabled(options.Level))
        {
            return;
        }

        var (timestamp, usedFallbackFormat) = ResolveTimestamp(options);
        if (usedFallbackFormat)
        {
            _defaultLogger.Debug(
                "Invalid common access log timestamp format '{TimestampFormat}' supplied – falling back to default.",
                options.TimestampFormat);
        }

        try
        {
            var logLine = BuildLogLine(context, options, elapsed, timestamp);
            logger.Write(options.Level, "{CommonAccessLogLine}", logLine);
        }
        catch (Exception ex)
        {
            // Access logging should never take down the pipeline – swallow and trace.
            _defaultLogger.Debug(ex, "Failed to emit common access log entry.");
        }
    }

    private Serilog.ILogger ResolveLogger(CommonAccessLogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Logger is { } customLogger ? CreateScopedLogger(customLogger) : _defaultLogger;
    }

    private static Serilog.ILogger CreateScopedLogger(Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return logger.ForContext("LogFormat", "CommonAccessLog")
                     .ForContext<CommonAccessLogMiddleware>();
    }

    private static string BuildLogLine(HttpContext context, CommonAccessLogOptions options, TimeSpan elapsed, string timestamp)
    {
        var request = context.Request;
        var response = context.Response;

        var remoteHost = SanitizeToken(ResolveClientAddress(context, options));
        var remoteIdent = "-"; // identd is rarely used – emit dash per the spec.
        var remoteUser = SanitizeToken(ResolveRemoteUser(context));

        var requestLine = SanitizeQuoted(BuildRequestLine(request, options));
        var statusCode = context.Response.StatusCode;
        var responseBytes = ResolveContentLength(response);
        var referer = SanitizeQuoted(GetHeaderValue(request.Headers, HeaderNames.Referer));
        var userAgent = SanitizeQuoted(GetHeaderValue(request.Headers, HeaderNames.UserAgent));

        // Pre-size the StringBuilder to avoid unnecessary allocations.
        var builder = new StringBuilder(remoteHost.Length
                                        + remoteUser.Length
                                        + requestLine.Length
                                        + referer.Length
                                        + userAgent.Length
                                        + 96);

        _ = builder.Append(remoteHost).Append(' ')
                   .Append(remoteIdent).Append(' ')
                   .Append(remoteUser).Append(" [")
                   .Append(timestamp).Append("] \"")
                   .Append(requestLine).Append("\" ")
                   .Append(statusCode.ToString(CultureInfo.InvariantCulture)).Append(' ')
                   .Append(responseBytes);

        _ = builder.Append(' ')
                   .Append('"').Append(referer).Append('"')
                   .Append(' ')
                   .Append('"').Append(userAgent).Append('"');

        if (options.IncludeElapsedMilliseconds)
        {
            var elapsedMs = elapsed.TotalMilliseconds.ToString("0.####", CultureInfo.InvariantCulture);
            _ = builder.Append(' ').Append(elapsedMs);
        }

        return builder.ToString();
    }

    private static (string Timestamp, bool UsedFallbackFormat) ResolveTimestamp(CommonAccessLogOptions options)
    {
        var provider = options.TimeProvider ?? TimeProvider.System;
        var timestamp = options.UseUtcTimestamp
            ? provider.GetUtcNow()
            : provider.GetLocalNow();

        var format = string.IsNullOrWhiteSpace(options.TimestampFormat)
            ? CommonAccessLogOptions.DefaultTimestampFormat
            : options.TimestampFormat;

        try
        {
            return (RenderTimestamp(timestamp, format), false);
        }
        catch (FormatException)
        {
            return (RenderTimestamp(timestamp, CommonAccessLogOptions.DefaultTimestampFormat), true);
        }
    }

    private static string RenderTimestamp(DateTimeOffset timestamp, string format)
    {
        var rendered = timestamp.ToString(format, CultureInfo.InvariantCulture);

        if (string.Equals(format, CommonAccessLogOptions.DefaultTimestampFormat, StringComparison.Ordinal))
        {
            var lastColon = rendered.LastIndexOf(':');
            if (lastColon >= 0 && lastColon >= rendered.Length - 5)
            {
                rendered = rendered.Remove(lastColon, 1);
            }
        }

        return rendered;
    }

    private static string ResolveClientAddress(HttpContext context, CommonAccessLogOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ClientAddressHeader)
            && context.Request.Headers.TryGetValue(options.ClientAddressHeader, out var forwarded))
        {
            var first = ExtractFirstHeaderValue(forwarded);
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return "-";
        }

        // Format IPv6 addresses without scope ID to match Apache behaviour.
        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? address.ScopeId == 0 ? address.ToString() : new IPAddress(address.GetAddressBytes()).ToString()
            : address.ToString();
    }

    private static string ExtractFirstHeaderValue(StringValues values)
    {
        if (StringValues.IsNullOrEmpty(values))
        {
            return "";
        }

        var span = values.ToString().AsSpan();
        var commaIndex = span.IndexOf(',');
        var first = commaIndex >= 0 ? span[..commaIndex] : span;
        return first.Trim().ToString();
    }

    private static string ResolveRemoteUser(HttpContext context)
    {
        var identity = context.User?.Identity;
        return identity is { IsAuthenticated: true, Name.Length: > 0 } ? identity.Name : "-";
    }

    private static string BuildRequestLine(HttpRequest request, CommonAccessLogOptions options)
    {
        var method = string.IsNullOrWhiteSpace(request.Method) ? "-" : request.Method;
        var path = request.Path.HasValue ? request.Path.Value : "/";
        if (string.IsNullOrEmpty(path))
        {
            path = "/";
        }

        if (options.IncludeQueryString && request.QueryString.HasValue)
        {
            path += request.QueryString.Value;
        }

        return options.IncludeProtocol && !string.IsNullOrWhiteSpace(request.Protocol)
            ? string.Concat(method, " ", path, " ", request.Protocol)
            : string.Concat(method, " ", path);
    }

    private static string ResolveContentLength(HttpResponse? response)
    {
        return response is null
            ? "-"
            : response.ContentLength.HasValue
            ? response.ContentLength.Value.ToString(CultureInfo.InvariantCulture)
            : response.Headers.ContentLength.HasValue ? response.Headers.ContentLength.Value.ToString(CultureInfo.InvariantCulture) : "-";
    }

    private static string GetHeaderValue(IHeaderDictionary headers, string headerName)
    {
        return !headers.TryGetValue(headerName, out var value)
            ? "-"
            : value.Count switch
            {
                0 => "-",
                1 => value[0] ?? "-",
                _ => value[0] ?? "-",
            };
    }

    private static string SanitizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "-")
        {
            return "-";
        }

        var span = value.AsSpan();
        var builder = new StringBuilder(span.Length);
        foreach (var ch in span)
        {
            if (char.IsControl(ch))
            {
                continue;
            }

            _ = char.IsWhiteSpace(ch) ? builder.Append('_') : ch == '"' ? builder.Append('_') : builder.Append(ch);
        }

        return builder.Length == 0 ? "-" : builder.ToString();
    }

    private static string SanitizeQuoted(string? value)
    {
        if (string.IsNullOrEmpty(value) || value == "-")
        {
            return "-";
        }

        var builder = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            if (ch is '\\' or '"')
            {
                _ = builder.Append('\\').Append(ch);
            }
            else if (ch is '\r' or '\n' || char.IsControl(ch))
            {
                continue;
            }
            else
            {
                _ = builder.Append(ch);
            }
        }

        return builder.Length == 0 ? "-" : builder.ToString();
    }
}
