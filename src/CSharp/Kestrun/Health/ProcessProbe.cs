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
        try
        {
            using var proc = CreateProcess();
            _ = proc.Start();
            var (outText, errText) = await RunProcessAsync(proc, ct).ConfigureAwait(false);

            return TryParseJsonContract(outText, out var contractResult) ? contractResult : MapExitCode(proc.ExitCode, errText);
        }
        catch (Exception ex)
        {
            return new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates and configures the process to be executed.
    /// </summary>
    private Process CreateProcess() => new()
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

    /// <summary>
    /// Runs the process and captures its standard output and error.
    /// </summary>
    /// <param name="proc">The process to run.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with the standard output and error as the result.</returns>
    private async Task<(string StdOut, string StdErr)> RunProcessAsync(Process proc, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        var stdOutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = proc.StandardError.ReadToEndAsync(ct);

        using var reg = cts.Token.Register(() =>
        {
            try { if (!proc.HasExited) { proc.Kill(true); } }
            catch { /* ignored */ }
        });

        _ = await Task.WhenAny(Task.Run(proc.WaitForExit, cts.Token), Task.Delay(Timeout.Infinite, cts.Token)).ConfigureAwait(false);

        var outText = await stdOutTask.ConfigureAwait(false);
        var errText = await stdErrTask.ConfigureAwait(false);
        return (outText, errText);
    }

    /// <summary>
    /// Parses the JSON contract from the process output.
    /// </summary>
    /// <param name="outText">The standard output text from the process.</param>
    /// <param name="result">The parsed probe result.</param>
    /// <returns>True if the JSON contract was successfully parsed; otherwise, false.</returns>
    private static bool TryParseJsonContract(string? outText, out ProbeResult result)
    {
        if (string.IsNullOrWhiteSpace(outText))
        {
            result = default!;
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(outText);
            if (!doc.RootElement.TryGetProperty("status", out var statusProp))
            {
                result = default!;
                return false;
            }

            var status = statusProp.GetString()?.ToLowerInvariant() switch
            {
                "healthy" => ProbeStatus.Healthy,
                "degraded" => ProbeStatus.Degraded,
                "unhealthy" => ProbeStatus.Unhealthy,
                _ => ProbeStatus.Unhealthy
            };

            var desc = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : null;
            var data = ParseJsonData(doc.RootElement);
            result = new ProbeResult(status, desc, data);
            return true;
        }
        catch
        {
            result = default!;
            return false;
        }
    }

    /// <summary>
    /// Parses the "data" property from the JSON root element into a dictionary.
    /// </summary>
    /// <param name="root">The root JSON element.</param>
    /// <returns>A dictionary containing the parsed data, or null if no data is present.</returns>
    private static IReadOnlyDictionary<string, object>? ParseJsonData(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var dataProp) || dataProp.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var dict = new Dictionary<string, object>();
        foreach (var p in dataProp.EnumerateObject())
        {
            dict[p.Name] = p.Value.ToString();
        }
        return dict.Count == 0 ? null : dict;
    }

    /// <summary>
    /// Maps the process exit code to a ProbeResult according to the health probe contract.
    /// </summary>
    /// <param name="code">The exit code of the process.</param>
    /// <param name="errText">The error text from the process output.</param>
    /// <returns>The mapped ProbeResult.</returns>
    private static ProbeResult MapExitCode(int code, string? errText)
    {
        var trimmedErr = string.IsNullOrWhiteSpace(errText) ? null : errText.Trim();
        return code switch
        {
            0 => new ProbeResult(ProbeStatus.Healthy, trimmedErr ?? "OK"),
            1 => new ProbeResult(ProbeStatus.Degraded, trimmedErr ?? "Degraded"),
            2 => new ProbeResult(ProbeStatus.Unhealthy, trimmedErr ?? "Unhealthy"),
            _ => new ProbeResult(ProbeStatus.Unhealthy, $"Exit {code}: {trimmedErr}".TrimEnd(':', ' '))
        };
    }
}
