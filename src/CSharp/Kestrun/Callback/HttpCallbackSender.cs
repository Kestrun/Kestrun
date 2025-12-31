namespace Kestrun.Callback;
/// <summary>
/// Sender for performing callback requests.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HttpCallbackSender"/> class.
/// </remarks>
/// <param name="http"> The HTTP client to use for sending requests.</param>
/// <param name="signer"> The optional callback signer for signing requests.</param>
public sealed class HttpCallbackSender(HttpClient http, ICallbackSigner? signer = null) : ICallbackSender
{
    private readonly HttpClient _http = http;
    private readonly ICallbackSigner? _signer = signer; // optional

    /// <summary>
    /// Sends a callback request via HTTP.
    /// </summary>
    /// <param name="r"> The callback request to send.</param>
    /// <param name="ct"> The cancellation token.</param>
    /// <returns> A task that represents the asynchronous operation, containing the callback result.</returns>
    public async Task<CallbackResult> SendAsync(CallbackRequest r, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(r.Timeout);

        using var msg = new HttpRequestMessage(new HttpMethod(r.HttpMethod), r.TargetUrl);

        foreach (var kv in r.Headers)
        {
            _ = msg.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        }

        _ = msg.Headers.TryAddWithoutValidation("X-Correlation-Id", r.CorrelationId);
        _ = msg.Headers.TryAddWithoutValidation("Idempotency-Key", r.IdempotencyKey);

        if (r.Body != null)
        {
            msg.Content = new ByteArrayContent(r.Body);
            msg.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(r.ContentType);
        }
        _signer?.Sign(msg, r); // HMAC signature etc.

        try
        {
            using var resp = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            var code = (int)resp.StatusCode;

            // treat 2xx as success
            if (code is >= 200 and <= 299)
            {
                return new CallbackResult(true, code, null, null, DateTimeOffset.UtcNow);
            }

            // read a small snippet for diagnostics (cap size)
            var err = resp.ReasonPhrase;
            return new CallbackResult(false, code, "HttpError", err, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException oce) when (!ct.IsCancellationRequested)
        {
            return new CallbackResult(false, null, "Timeout", oce.Message, DateTimeOffset.UtcNow);
        }
        catch (HttpRequestException hre)
        {
            return new CallbackResult(false, null, "HttpRequestException", hre.Message, DateTimeOffset.UtcNow);
        }
    }
}
