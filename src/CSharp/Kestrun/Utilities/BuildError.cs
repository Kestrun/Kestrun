using System.Management.Automation;
using System.Text;
using Serilog;

namespace Kestrun.Utilities;

/// <summary>
/// Utilities for formatting PowerShell error streams into HTTP responses.
/// </summary>
public static class BuildError
{
    /// <summary>
    /// Convert the current PowerShell error streams to a plain-text <see cref="IResult"/>.
    /// </summary>
    public static IResult Result(PowerShell ps) => Results.Text(content: Text(ps), statusCode: 500, contentType: "text/plain; charset=utf-8");



    /// <summary>
    /// Collate all PowerShell streams (error, verbose, warning, etc.) into a single string.
    /// </summary>
    public static string Text(PowerShell ps)
    {
        ArgumentNullException.ThrowIfNull(ps);

        var errors = ps.Streams.Error.Select(e => e.ToString());
        var verbose = ps.Streams.Verbose.Select(v => v.ToString());
        var warnings = ps.Streams.Warning.Select(w => w.ToString());
        var debug = ps.Streams.Debug.Select(d => d.ToString());
        var info = ps.Streams.Information.Select(i => i.ToString());
        // Format the output
        // 500 + text body

        var sb = new StringBuilder();

        void append(string emoji, IEnumerable<string> lines)
        {
            if (!lines.Any())
            {
                return;
            }

            _ = sb.AppendLine($"{emoji}[{emoji switch
            {
                "❌" => "Error",
                "💬" => "Verbose",
                "⚠️" => "Warning",
                "🐞" => "Debug",
                _ => "Info"
            }}]");
            foreach (var l in lines)
            {
                _ = sb.AppendLine($"\t{l}");
            }
        }

        append("❌", errors);
        append("💬", verbose);
        append("⚠️", warnings);
        append("🐞", debug);
        append("ℹ️", info);

        var msg = sb.ToString();
        Log.Information(msg);


        return msg;
    }

    // Helper that writes the error to the response stream
    /// <summary>
    /// Write the formatted PowerShell errors directly to the HTTP response.
    /// </summary>
    public static Task ResponseAsync(HttpContext context, PowerShell ps)
    {
        var errText = Text(ps);
        context.Response.StatusCode = 500;
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync(errText);
    }
}
