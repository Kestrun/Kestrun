using System.Text.Json;
using Serilog.Events;

namespace Kestrun.Health;

/// <summary>
/// A health probe that performs an HTTP GET request to a specified URL and interprets the JSON
/// response according to the health probe contract.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HttpProbe"/> class.
/// </remarks>
/// <param name="name">The name of the probe.</param>
/// <param name="tags">The tags associated with the probe.</param>
/// <param name="http">The HTTP client to use.</param>
/// <param name="url">The URL to probe.</param>
/// <param name="timeout">The timeout for the probe.</param>
/// <param name="logger">Optional logger; if null a contextual logger is created.</param>
public sealed class HttpProbe(string name, string[] tags, HttpClient http, string url, TimeSpan? timeout = null, Serilog.ILogger? logger = null) : IProbe
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
    public Serilog.ILogger Logger { get; init; } = logger ?? Serilog.Log.ForContext("HealthProbe", name);
    /// <summary>
    /// The HTTP client to use.
    /// </summary>
    private readonly HttpClient _http = http;
    private readonly string _url = url;
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(5);

    /// <summary>
    /// Executes the HTTP GET request and interprets the response according to the health probe contract.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a <see cref="ProbeResult"/> as the result.</returns>
    public async Task<ProbeResult> CheckAsync(CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        try
        {
            var (response, body) = await ExecuteHttpRequestAsync(cts.Token).ConfigureAwait(false);
            return ParseHealthResponse(response, body);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Upstream/request cancellation -> propagate so the runner can handle overall request abort semantics.
            throw;
        }
        catch (TaskCanceledException) when (cts.Token.IsCancellationRequested) // timeout from our internal cts
        {
            return HandleTimeout();
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested) // internal timeout (already handled TaskCanceled, but just in case)
        {
            return HandleTimeout();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "HttpProbe {Probe} failed", Name);
            return new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the HTTP GET request and returns the response and body.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple containing the HTTP response and response body.</returns>
    private async Task<(HttpResponseMessage Response, string? Body)> ExecuteHttpRequestAsync(CancellationToken cancellationToken)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("HttpProbe {Probe} sending GET {Url} (timeout={Timeout})", Name, _url, _timeout);
        }

        var response = await _http.GetAsync(_url, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("HttpProbe {Probe} received {StatusCode} length={Length}", Name, (int)response.StatusCode, body?.Length ?? 0);
        }

        return (response, body);
    }

    /// <summary>
    /// Parses the HTTP response and determines the health status.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="body">The response body.</param>
    /// <returns>The probe result.</returns>
    private ProbeResult ParseHealthResponse(HttpResponseMessage response, string? body)
    {
        var contractResult = TryParseHealthContract(body);
        if (contractResult != null)
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("HttpProbe {Probe} parsed contract status={Status}", Name, contractResult.Status);
            }
            return contractResult;
        }

        return HandleNonContractResponse(response);
    }

    /// <summary>
    /// Attempts to parse the response body as a health contract JSON.
    /// </summary>
    /// <param name="body">The response body.</param>
    /// <returns>The probe result if parsing succeeds, null otherwise.</returns>
    private ProbeResult? TryParseHealthContract(string? body)
    {
        try
        {
            var doc = JsonDocument.Parse(body ?? string.Empty);
            var statusStr = doc.RootElement.GetProperty("status").GetString();
            var status = statusStr?.ToLowerInvariant() switch
            {
                ProbeStatusLabels.STATUS_HEALTHY => ProbeStatus.Healthy,
                ProbeStatusLabels.STATUS_DEGRADED => ProbeStatus.Degraded,
                ProbeStatusLabels.STATUS_UNHEALTHY => ProbeStatus.Unhealthy,
                _ => ProbeStatus.Unhealthy
            };
            var desc = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : null;
            return new ProbeResult(status, desc, null);
        }
        catch
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("HttpProbe {Probe} response body is not valid contract JSON", Name);
            }
            return null;
        }
    }

    /// <summary>
    /// Handles responses that don't conform to the health contract.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <returns>The probe result.</returns>
    private ProbeResult HandleNonContractResponse(HttpResponseMessage response)
    {
        var result = response.IsSuccessStatusCode
            ? new ProbeResult(ProbeStatus.Degraded, "No contract JSON")
            : new ProbeResult(ProbeStatus.Unhealthy, $"HTTP {(int)response.StatusCode}");

        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("HttpProbe {Probe} non-contract response mapped to {Status}", Name, result.Status);
        }

        return result;
    }

    /// <summary>
    /// Handles timeout scenarios.
    /// </summary>
    /// <returns>The probe result for timeout.</returns>
    private ProbeResult HandleTimeout()
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("HttpProbe {Probe} timed out after {Timeout}", Name, _timeout);
        }
        return new ProbeResult(ProbeStatus.Degraded, $"Timeout after {_timeout}");
    }
}
