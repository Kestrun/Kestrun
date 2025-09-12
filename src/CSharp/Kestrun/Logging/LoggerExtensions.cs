
using System.Text;
using Serilog.Events;


namespace Kestrun.Logging;

/// <summary>
/// Sanitized Serilog extensions to strip control chars (including CR/LF)
/// from any string property values before writing the log.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Writes a sanitized debug log event, removing control characters from string property values.
    /// </summary>
    /// <param name="log">The Serilog logger instance.</param>
    /// <param name="messageTemplate">The message template.</param>
    /// <param name="propertyValues">The property values for the message template.</param>
    public static void DebugSanitized(this Serilog.ILogger log, string messageTemplate, params object?[] propertyValues)
    {
        if (!log.IsEnabled(LogEventLevel.Debug))
        {
            return;
        }

        var sanitized = propertyValues.Select(SanitizeObject).ToArray();
        log.Debug(messageTemplate, sanitized);
    }


    /// <summary>
    /// Writes a sanitized debug log event with an exception, removing control characters from string property values.
    /// </summary>
    /// <param name="log">The Serilog logger instance.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="messageTemplate">The message template.</param>
    /// <param name="propertyValues">The property values for the message template.</param>
    public static void DebugSanitized(this Serilog.ILogger log, Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        if (!log.IsEnabled(LogEventLevel.Debug))
        {
            return;
        }

        var sanitized = propertyValues.Select(SanitizeObject).ToArray();
        log.Debug(exception, messageTemplate, sanitized);
    }

    // Helper: sanitize only string args
    private static object? SanitizeObject(object? o) =>
        o is string s
            ? SanitizeString(s)
            : o;

    // Strip out all control characters (0x00–0x1F, 0x7F), including CR/LF
    private static string SanitizeString(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (char.IsControl(c))
            {
                continue;
            }

            _ = sb.Append(c);
        }
        return sb.ToString();
    }
}
