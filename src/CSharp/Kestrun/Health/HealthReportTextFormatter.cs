using System.Text;

namespace Kestrun.Health;

/// <summary>
/// Produces a concise, human-readable text representation of a <see cref="HealthReport"/>.
/// Intended for terminals, logs, or lightweight probes where structured formats (JSON/YAML/XML)
/// are unnecessary.
/// </summary>
public static class HealthReportTextFormatter
{
    /// <summary>
    /// Formats a <see cref="HealthReport"/> into a concise plain text representation. Includes probe data when <paramref name="includeData"/> is true.
    /// </summary>
    /// <param name="report">The health report to format.</param>
    /// <param name="includeData">When true (default) emits per-probe data key/value lines.</param>
    /// <returns>A multi-line string suitable for console or log output.</returns>
    public static string Format(HealthReport report, bool includeData = true)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sb = new StringBuilder();
        _ = sb.AppendLine($"Status: {report.StatusText}");
        _ = sb.AppendLine($"GeneratedAt: {report.GeneratedAt:O}");
        var s = report.Summary;
        _ = sb.AppendLine($"Summary: total={s.Total} healthy={s.Healthy} degraded={s.Degraded} unhealthy={s.Unhealthy}");
        if (report.AppliedTags is { Count: > 0 })
        {
            _ = sb.AppendLine($"Tags: {string.Join(",", report.AppliedTags)}");
        }
        _ = sb.AppendLine("Probes:");
        foreach (var p in report.Probes)
        {
            _ = sb.Append("  - ");
            _ = sb.Append($"name={p.Name} status={p.StatusText} duration={FormatDuration(p.Duration)}");
            if (!string.IsNullOrWhiteSpace(p.Description))
            {
                _ = sb.Append($" desc=\"{Escape(p.Description)}\"");
            }
            if (!string.IsNullOrWhiteSpace(p.Error))
            {
                _ = sb.Append($" error=\"{Escape(p.Error)}\"");
            }
            _ = sb.AppendLine();
            if (includeData && p.Data is { Count: > 0 })
            {
                foreach (var kvp in p.Data)
                {
                    _ = sb.AppendLine($"      {kvp.Key}={RenderValue(kvp.Value)}");
                }
            }
        }
        return sb.ToString();
    }

    private static string RenderValue(object? value)
    {
        return value is null
            ? "<null>"
            : value switch
            {
                string s => '"' + Escape(s) + '"',
                DateTime dt => dt.ToString("O"),
                DateTimeOffset dto => dto.ToString("O"),
                TimeSpan ts => ts.ToString(),
                _ => value.ToString() ?? string.Empty
            };
    }

    private static string Escape(string input) => input.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalMilliseconds < 1
            ? "<1ms"
            : duration.TotalMilliseconds < 1000
            ? ((int)duration.TotalMilliseconds) + "ms"
            : duration.TotalSeconds < 60 ? duration.TotalSeconds.ToString("0.###") + "s" : duration.ToString();
    }
}
