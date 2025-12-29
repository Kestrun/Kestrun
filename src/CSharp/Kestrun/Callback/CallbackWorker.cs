
using System.Threading.Channels;

namespace Kestrun.Callback;

/// <summary>
/// Background worker that processes callback requests from the queue.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CallbackWorker"/> class.
/// </remarks>
/// <param name="queue"> The in-memory callback queue.</param>
/// <param name="sender"> The callback sender.</param>
/// <param name="retry"> The callback retry policy.</param>
/// <param name="log"> The logger.</param>
/// <param name="store"> The optional callback store.</param>
public sealed class CallbackWorker(
    InMemoryCallbackQueue queue,
    ICallbackSender sender,
    ICallbackRetryPolicy retry,
    ILogger<CallbackWorker> log,
    ICallbackStore? store = null) : BackgroundService
{
    private readonly ChannelReader<CallbackRequest> _reader = queue.Channel.Reader;
    private readonly ICallbackSender _sender = sender;
    private readonly ICallbackRetryPolicy _retry = retry;
    private readonly ICallbackStore? _store = store; // optional
    private readonly ILogger<CallbackWorker> _log = log;

    /// <summary>
    /// Executes the background service.
    /// </summary>
    /// <param name="stoppingToken"> Cancellation token to stop the service.</param>
    /// <returns> A task that represents the background service execution.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var req in _reader.ReadAllAsync(stoppingToken))
        {
            // Fire-and-limit concurrency via Task.Run? Better: use a SemaphoreSlim
            _ = ProcessOne(req, stoppingToken);
        }
    }

    /// <summary>
    /// Processes a single callback request.
    /// </summary>
    /// <param name="req"> The callback request to process.</param>
    /// <param name="ct">  The cancellation token.</param>
    /// <returns> A task that represents the asynchronous operation.</returns>
    private async Task ProcessOne(CallbackRequest req, CancellationToken ct)
    {
        try
        {
            if (_store is not null)
            {
                await _store.MarkInFlightAsync(req, ct);
            }

            var result = await _sender.SendAsync(req, ct);

            if (result.Success)
            {
                if (_store is not null)
                {
                    await _store.MarkSucceededAsync(req, result, ct);
                }

                return;
            }

            await HandleFailure(req, result, ct);
        }
        catch (Exception ex)
        {
            var result = new CallbackResult(false, null, ex.GetType().Name, ex.Message, DateTimeOffset.UtcNow);
            await HandleFailure(req, result, ct);
        }
    }

    /// <summary>
    /// Handles a failed callback request.
    /// </summary>
    /// <param name="req"> The callback request that failed.</param>
    /// <param name="result"> The result of the callback attempt.</param>
    /// <param name="ct"> The cancellation token.</param>
    /// <returns> A task that represents the asynchronous operation.</returns>
    private async Task HandleFailure(CallbackRequest req, CallbackResult result, CancellationToken ct)
    {
        var decision = _retry.Evaluate(req, result);

        if (decision.Kind == RetryDecisionKind.Retry)
        {
            req.Attempt++;
            req.NextAttemptAt = decision.NextAttemptAt;


            if (_store is not null)
            {
                await _store.MarkRetryScheduledAsync(req, result, ct);
            }

            // schedule re-enqueue (simple in-memory) or rely on durable poller
            _ = Task.Delay(decision.Delay, ct).ContinueWith(async _ =>
            {
                // ignore exceptions if shutting down
                try { await EnqueueAgain(req, ct); } catch { }
            }, TaskScheduler.Default);

            return;
        }

        if (_store is not null)
        {
            await _store.MarkFailedPermanentAsync(req, result, ct);
        }

        _log.LogWarning("Callback failed permanently {CallbackId} after {Attempts} attempts. Last error: {Err}",
            req.CallbackId, req.Attempt + 1, result.ErrorMessage);
    }

    /// <summary>
    /// Enqueues the callback request again for retry.
    /// </summary>
    /// <param name="req"> The callback request to enqueue again.</param>
    /// <param name="ct"> The cancellation token.</param>
    /// <returns> A task that represents the asynchronous operation.</returns>
    /// <exception cref="NotImplementedException"></exception>
    private async Task EnqueueAgain(CallbackRequest req, CancellationToken ct) =>
        // if using in-memory, you need access to writer; if durable, you don't.
        // assume in-memory for this snippet.
        throw new NotImplementedException();
}
