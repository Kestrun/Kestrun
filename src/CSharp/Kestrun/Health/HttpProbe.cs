using System.Text.Json;

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
public sealed class HttpProbe(string name, string[] tags, HttpClient http, string url, TimeSpan? timeout = null) : IProbe
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
            var rsp = await _http.GetAsync(_url, cts.Token);
            var body = await rsp.Content.ReadAsStringAsync(cts.Token);

            try
            {
                var doc = JsonDocument.Parse(body);
                var statusStr = doc.RootElement.GetProperty("status").GetString();
                var status = statusStr?.ToLowerInvariant() switch
                {
                    "healthy" => ProbeStatus.Healthy,
                    "degraded" => ProbeStatus.Degraded,
                    "unhealthy" => ProbeStatus.Unhealthy,
                    _ => ProbeStatus.Unhealthy
                };
                var desc = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : null;
                return new ProbeResult(status, desc, null);
            }
            catch
            {
                // Non-contract response: degrade on 200, unhealthy otherwise
                return rsp.IsSuccessStatusCode
                    ? new ProbeResult(ProbeStatus.Degraded, "No contract JSON")
                    : new ProbeResult(ProbeStatus.Unhealthy, $"HTTP {(int)rsp.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            return new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}");
        }
    }
}
