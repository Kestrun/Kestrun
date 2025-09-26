using System.Diagnostics;
using System.Text.Json;


namespace Kestrun.Health;
/// <summary>
/// A health probe that runs an external process and interprets its output.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ProcessProbe"/> class.
/// </remarks>
/// <param name="name">The name of the probe.</param>
/// <param name="tags">The tags associated with the probe.</param>
/// <param name="fileName">The file name of the process to run.</param>
/// <param name="args">The arguments to pass to the process.</param>
/// <param name="timeout">The timeout for the process to complete.</param>
public sealed class ProcessProbe(string name, string[] tags, string fileName, string args = "", TimeSpan? timeout = null) : IProbe
{
    /// <summary>
    /// The name of the probe.
    /// </summary>
    public string Name { get; } = name;
    /// <summary>
    /// The tags associated with the probe.
    /// </summary>
    public string[] Tags { get; } = tags;
    /// <summary>
    /// The file name of the process to run.
    /// </summary>
    private readonly string _fileName = fileName;
    /// <summary>
    /// The arguments to pass to the process.
    /// </summary>
    private readonly string _args = args ?? "";
    /// <summary>
    /// The timeout for the process to complete.
    /// </summary>
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(10);

    /// <summary>
    /// Executes the process and interprets its output according to the health probe contract.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a <see cref="ProbeResult"/> as the result.</returns>
    public async Task<ProbeResult> CheckAsync(CancellationToken ct = default)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _fileName,
                Arguments = _args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        try
        {
            _ = proc.Start();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeout);

            var stdOutTask = proc.StandardOutput.ReadToEndAsync();
            var stdErrTask = proc.StandardError.ReadToEndAsync();

            // Wait for exit or timeout
            using var reg = cts.Token.Register(() => { try { if (!proc.HasExited) { proc.Kill(true); } } catch { } });
            _ = await Task.WhenAny(Task.Run(proc.WaitForExit, cts.Token), Task.Delay(Timeout.Infinite, cts.Token));

            var outText = await stdOutTask;
            var errText = await stdErrTask;

            // Try JSON contract first
            if (!string.IsNullOrWhiteSpace(outText))
            {
                try
                {
                    var doc = JsonDocument.Parse(outText);
                    var statusStr = doc.RootElement.GetProperty("status").GetString();
                    var status = statusStr?.ToLowerInvariant() switch
                    {
                        "healthy" => ProbeStatus.Healthy,
                        "degraded" => ProbeStatus.Degraded,
                        "unhealthy" => ProbeStatus.Unhealthy,
                        _ => ProbeStatus.Unhealthy
                    };

                    var desc = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : null;
                    IReadOnlyDictionary<string, object>? data = null;
                    if (doc.RootElement.TryGetProperty("data", out var dd) && dd.ValueKind == JsonValueKind.Object)
                    {
                        var dict = new Dictionary<string, object>();
                        foreach (var p in dd.EnumerateObject())
                        {
                            dict[p.Name] = p.Value.ToString();
                        }

                        data = dict;
                    }
                    return new ProbeResult(status, desc, data);
                }
                catch { /* fall back to exit code */ }
            }

            // Exit code fallback
            var code = proc.ExitCode;
            return code switch
            {
                0 => new ProbeResult(ProbeStatus.Healthy, string.IsNullOrWhiteSpace(errText) ? "OK" : errText.Trim()),
                1 => new ProbeResult(ProbeStatus.Degraded, string.IsNullOrWhiteSpace(errText) ? "Degraded" : errText.Trim()),
                2 => new ProbeResult(ProbeStatus.Unhealthy, string.IsNullOrWhiteSpace(errText) ? "Unhealthy" : errText.Trim()),
                _ => new ProbeResult(ProbeStatus.Unhealthy, $"Exit {code}: {errText}".Trim())
            };
        }
        catch (Exception ex)
        {
            return new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}");
        }
    }
}
