
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
    Serilog.ILogger log,
    ICallbackStore? store = null) : BackgroundService
{
    private readonly ChannelReader<CallbackRequest> _reader = queue.Channel.Reader;
    private readonly ChannelWriter<CallbackRequest> _writer = queue.Channel.Writer;
    private readonly ICallbackSender _sender = sender;
    private readonly ICallbackRetryPolicy _retry = retry;
    private readonly ICallbackStore? _store = store; // optional
    private readonly Serilog.ILogger _log = log;

    /// <summary>
    /// Executes the background service.
    /// </summary>
    /// <param name="stoppingToken"> Cancellation token to stop the service.</param>
    /// <returns> A task that represents the background service execution.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var req in _reader.ReadAllAsync(stoppingToken))
            {
                // Fire-and-limit concurrency via Task.Run? Better: use a SemaphoreSlim
                _ = ProcessOne(req, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
            if (_log.IsEnabled(Serilog.Events.LogEventLevel.Information))
            {
                _log.Information("CallbackWorker is stopping due to cancellation.");
            }
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
                try { await EnqueueAgain(req, ct); }
                catch (OperationCanceledException)
                {
                    // expected during shutdown; no further action required
                    if (_log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                    {
                        _log.Debug("Callback {CallbackId} re-enqueue skipped due to shutdown.",
                            req.CallbackId);
                    }
                }
                catch (Exception ex)
                {
                    if (_log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                    {
                        _log.Debug(ex,
                            "Failed to re-enqueue callback {CallbackId} during retry scheduling.",
                            req.CallbackId);
                    }
                }
            }, TaskScheduler.Default);

            return;
        }

        if (_store is not null)
        {
            await _store.MarkFailedPermanentAsync(req, result, ct);
        }

        _log.Warning("Callback failed permanently {CallbackId} after {Attempts} attempts. Last error: {Err}",
            req.CallbackId, req.Attempt + 1, result.ErrorMessage);
    }

    /// <summary>
    /// Enqueues the callback request again for retry.
    /// </summary>
    /// <param name="req"> The callback request to enqueue again.</param>
    /// <param name="ct"> The cancellation token.</param>
    /// <returns> A task that represents the asynchronous operation.</returns>
    private async Task EnqueueAgain(CallbackRequest req, CancellationToken ct)
        => await _writer.WriteAsync(req, ct).ConfigureAwait(false);
}
