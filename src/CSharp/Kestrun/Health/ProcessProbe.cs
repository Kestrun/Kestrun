using System.Diagnostics;
using System.Text.Json;
using Serilog.Events;
using Serilog;

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
/// <param name="logger">Optional logger; if null a contextual logger is created.</param>
public sealed class ProcessProbe(string name, string[] tags, string fileName, string args = "", TimeSpan? timeout = null, Serilog.ILogger? logger = null) : IProbe
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
    /// Logger used for diagnostics.
    /// </summary>
    public Serilog.ILogger Logger { get; init; } = logger ?? Log.ForContext("HealthProbe", name);
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
        var sw = Stopwatch.StartNew();
        try
        {
            using var proc = CreateProcess();
            _ = proc.Start();
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("ProcessProbe {Probe} started process {File} {Args} (PID={Pid}) with timeout {Timeout}", Name, _fileName, _args, proc.Id, _timeout);
            }
            var (outText, errText, timedOut) = await RunProcessAsync(proc, ct).ConfigureAwait(false);
            if (timedOut)
            {
                sw.Stop();
                if (Logger.IsEnabled(LogEventLevel.Debug))
                {
                    Logger.Debug("ProcessProbe {Probe} internal timeout after {Timeout} (duration={Duration}ms)", Name, _timeout, sw.ElapsedMilliseconds);
                }
                var data = string.IsNullOrWhiteSpace(outText)
                    ? null
                    : new Dictionary<string, object> { ["stdout"] = outText.Length > 500 ? outText[..500] : outText };
                return new ProbeResult(ProbeStatus.Degraded, $"Timed out after {_timeout.TotalMilliseconds}ms", data);
            }
            sw.Stop();
            if (TryParseJsonContract(outText, out var contractResult))
            {
                if (Logger.IsEnabled(LogEventLevel.Debug))
                {
                    Logger.Debug("ProcessProbe {Probe} parsed JSON contract (exit={ExitCode}, duration={Duration}ms)", Name, proc.ExitCode, sw.ElapsedMilliseconds);
                }
                return contractResult;
            }
            var mapped = MapExitCode(proc.ExitCode, errText);
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("ProcessProbe {Probe} completed (exit={ExitCode}, status={Status}, duration={Duration}ms)", Name, proc.ExitCode, mapped.Status, sw.ElapsedMilliseconds);
            }
            return mapped;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Surface caller/request cancellation without converting to a health status; runner decides final response.
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // Internal timeout (cts.CancelAfter) -> degrade instead of unhealthy so transient slowness isn't reported as outright failure.
            sw.Stop();
            Logger.Warning(ex, "ProcessProbe {Probe} timed out after {Timeout} (duration={Duration}ms)", Name, _timeout, sw.ElapsedMilliseconds);
            return new ProbeResult(ProbeStatus.Degraded, $"Timed out: {ex.Message}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Logger.Error(ex, "ProcessProbe {Probe} failed after {Duration}ms", Name, sw.ElapsedMilliseconds);
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
    private async Task<(string StdOut, string StdErr, bool TimedOut)> RunProcessAsync(Process proc, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        var stdOutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = proc.StandardError.ReadToEndAsync(ct);

        using var reg = SetupProcessKillRegistration(proc, cts.Token);
        var timedOut = await WaitForProcessWithTimeout(proc, cts.Token, ct);
        var (outText, errText) = await ReadProcessStreams(stdOutTask, stdErrTask, timedOut, ct);

        return (outText, errText, timedOut);
    }

    /// <summary>
    /// Sets up a cancellation token registration to kill the process when timeout or cancellation occurs.
    /// </summary>
    /// <param name="proc">The process to potentially kill.</param>
    /// <param name="cancellationToken">The token that triggers the kill operation.</param>
    /// <returns>The cancellation token registration.</returns>
    private CancellationTokenRegistration SetupProcessKillRegistration(Process proc, CancellationToken cancellationToken)
    {
        return cancellationToken.Register(() =>
        {
            try
            {
                if (!proc.HasExited)
                {
                    if (Logger.IsEnabled(LogEventLevel.Debug))
                    {
                        Logger.Debug("ProcessProbe {Probe} cancel/timeout -> killing PID {Pid}", Name, proc.Id);
                    }
                    proc.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited, safe to ignore.
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "ProcessProbe {Probe} exception while attempting to kill PID {Pid}", Name, proc.Id);
            }
        });
    }

    /// <summary>
    /// Waits for the process to exit, handling timeout scenarios.
    /// </summary>
    /// <param name="proc">The process to wait for.</param>
    /// <param name="timeoutToken">Token that fires on timeout.</param>
    /// <param name="callerToken">The original caller's cancellation token.</param>
    /// <returns>True if the process timed out, false if it completed normally.</returns>
    private static async Task<bool> WaitForProcessWithTimeout(Process proc, CancellationToken timeoutToken, CancellationToken callerToken)
    {
        try
        {
            await proc.WaitForExitAsync(timeoutToken).ConfigureAwait(false);
            return false; // No timeout
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            // Internal timeout fired; process kill requested via registration. Mark and wait for real exit to drain streams.
            try
            {
                proc.WaitForExit(); // ensure fully exited so streams close
            }
            catch (Exception ex) when (ex is InvalidOperationException)
            {
                // ignore - already exited
            }
            return true; // Timed out
        }
    }

    /// <summary>
    /// Reads the process stdout and stderr streams with error handling.
    /// </summary>
    /// <param name="stdOutTask">Task reading standard output.</param>
    /// <param name="stdErrTask">Task reading standard error.</param>
    /// <param name="timedOut">Whether the process timed out.</param>
    /// <param name="callerToken">The original caller's cancellation token.</param>
    /// <returns>The stdout and stderr text.</returns>
    private static async Task<(string StdOut, string StdErr)> ReadProcessStreams(
        Task<string> stdOutTask,
        Task<string> stdErrTask,
        bool timedOut,
        CancellationToken callerToken)
    {
        var outText = string.Empty;
        var errText = string.Empty;

        try
        {
            outText = await stdOutTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timedOut && !callerToken.IsCancellationRequested)
        {
            // stdout read was canceled by caller token? (should not happen since we only passed caller token)
        }

        try
        {
            errText = await stdErrTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timedOut && !callerToken.IsCancellationRequested)
        {
            // ignore similar to above
        }

        return (outText, errText);
    }

    /// <summary>
    /// Parses the JSON contract from the process output.
    /// </summary>
    /// <param name="outText">The standard output text from the process.</param>
    /// <param name="result">The parsed probe result.</param>
    /// <returns>True if the JSON contract was successfully parsed; otherwise, false.</returns>
    private bool TryParseJsonContract(string? outText, out ProbeResult result)
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
        catch (Exception ex)
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug(ex, "ProcessProbe {Probe} output not valid contract JSON", Name);
            }
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
