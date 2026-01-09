using System.Text;

namespace Kestrun.Sse;

/// <summary>
/// Formats Server-Sent Events (SSE) payloads according to the SSE wire format.
/// </summary>
public static class SseEventFormatter
{
    /// <summary>
    /// Formats a single SSE event payload.
    /// </summary>
    /// <param name="eventName">Optional event name.</param>
    /// <param name="data">Event payload data (may be multi-line).</param>
    /// <param name="id">Optional event ID.</param>
    /// <param name="retryMs">Optional reconnect interval in milliseconds.</param>
    /// <returns>A formatted SSE payload string.</returns>
    public static string Format(string? eventName, string data, string? id = null, int? retryMs = null)
    {
        var sb = new StringBuilder(capacity: Math.Max(64, data.Length + 32));

        if (retryMs is not null)
        {
            _ = sb.Append("retry: ").Append(retryMs.Value).Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(id))
        {
            _ = sb.Append("id: ").Append(id).Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(eventName))
        {
            _ = sb.Append("event: ").Append(eventName).Append('\n');
        }

        using (var sr = new StringReader(data))
        {
            string? line;
            while ((line = sr.ReadLine()) is not null)
            {
                _ = sb.Append("data: ").Append(line).Append('\n');
            }
        }

        _ = sb.Append('\n');
        return sb.ToString();
    }

    /// <summary>
    /// Formats an SSE comment payload (useful for keep-alives).
    /// </summary>
    /// <param name="comment">Comment text.</param>
    /// <returns>A formatted SSE comment payload string.</returns>
    public static string FormatComment(string comment) =>
        // Comment lines start with ':'
        $": {comment}\n\n";
}
