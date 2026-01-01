using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace Kestrun.Callback;

/// <summary>
/// In-memory implementation of <see cref="ICallbackDispatcher"/>.
/// Enqueues callback requests into an in-memory queue for processing.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="InMemoryCallbackDispatchWorker"/> class.
/// </remarks>
/// <param name="queue">The in-memory callback queue.</param>
/// <param name="httpClientFactory">The HTTP client factory.</param>
/// <param name="log">The logger instance.</param>
public sealed class InMemoryCallbackDispatchWorker(
    InMemoryCallbackQueue queue,
    IHttpClientFactory httpClientFactory,
    Serilog.ILogger log) : BackgroundService
{
    private readonly InMemoryCallbackQueue _queue = queue;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly Serilog.ILogger _log = log;

    /// <summary>
    /// Executes the callback dispatching process.
    /// </summary>
    /// <param name="stoppingToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var httpClient = _httpClientFactory.CreateClient("kestrun-callbacks");

        while (!stoppingToken.IsCancellationRequested)
        {
            CallbackRequest req;
            try
            {
                req = await _queue.Channel.Reader.ReadAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await DispatchOneAsync(httpClient, req, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Callback dispatch failed. CallbackId={CallbackId} Url={Url}",
                    req.CallbackId, req.TargetUrl);

                // TODO: retry / dead-letter policy
            }
        }
    }

    /// <summary>
    /// Dispatches a single callback request.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to send the request.</param>
    /// <param name="req">The callback request to be dispatched.</param>
    /// <param name="token">The cancellation token to observe.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task DispatchOneAsync(HttpClient httpClient, CallbackRequest req, CancellationToken token)
    {
        using var msg = new HttpRequestMessage(new HttpMethod(req.HttpMethod), req.TargetUrl);

        if (req.Body is not null)
        {
            msg.Content = new ByteArrayContent(req.Body);
            // Set Content-Type explicitly (avoids overload confusion)
            if (!string.IsNullOrWhiteSpace(req.ContentType))
            {
                msg.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(req.ContentType);
            }
        }

        foreach (var h in req.Headers)
        {
            // Content headers must go on Content, not on msg.Headers
            if (!msg.Headers.TryAddWithoutValidation(h.Key, h.Value))
            {
                _ = (msg.Content?.Headers.TryAddWithoutValidation(h.Key, h.Value));
            }
        }

        _log.Information("Sending callback. CallbackId={CallbackId} Url={Url}", req.CallbackId, req.TargetUrl);

        using var resp = await SendCallbackAsync(httpClient, msg, token).ConfigureAwait(false);

        if ((int)resp.StatusCode >= 500)
        {
            var body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            _log.Warning("Callback got {StatusCode} (server error). CallbackId={CallbackId} Url={Url} BodySnippet={Body}",
                (int)resp.StatusCode, req.CallbackId, req.TargetUrl, Snip(body, 500));

            // TODO: retry
            return;
        }

        if ((int)resp.StatusCode >= 400)
        {
            var body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            _log.Warning("Callback rejected {StatusCode}. CallbackId={CallbackId} Url={Url} BodySnippet={Body}",
                (int)resp.StatusCode, req.CallbackId, req.TargetUrl, Snip(body, 500));

            // TODO: dead-letter (usually)
            return;
        }

        _log.Information("Callback delivered. CallbackId={CallbackId} Status={StatusCode}",
            req.CallbackId, (int)resp.StatusCode);
    }

    private async Task<HttpResponseMessage> SendCallbackAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        CancellationToken token)
    {
        try
        {
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                                   .ConfigureAwait(false);
        }
        catch (TaskCanceledException ex)
        {
            _log.Warning(ex, "Callback timed out. Url={Url}", request.RequestUri);
            throw;
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException se)
        {
            _log.Error(ex, "Callback DNS/connect failure. SocketError={SocketError} Url={Url}",
                se.SocketErrorCode, request.RequestUri);
            throw;
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException or IOException)
        {
            _log.Error(ex, "Callback TLS/SSL failure. Url={Url}", request.RequestUri);
            throw;
        }
        catch (HttpRequestException ex)
        {
            _log.Error(ex, "Callback HTTP request failure. Url={Url}", request.RequestUri);
            throw;
        }
    }

    private static string Snip(string? s, int max)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max]);
}
