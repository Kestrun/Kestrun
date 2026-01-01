namespace Kestrun.Callback;

/// <summary>
/// In-memory dispatcher for enqueuing callback requests.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="InMemoryCallbackDispatcher"/> class.
/// </remarks>
/// <param name="queue">The in-memory callback queue to use for dispatching callbacks.</param>
public sealed class InMemoryCallbackDispatcher(InMemoryCallbackQueue queue) : ICallbackDispatcher
{
    private readonly InMemoryCallbackQueue _queue = queue;

    /// <summary>
    /// Enqueue a callback request for processing.
    /// </summary>
    /// <param name="request">The callback request to enqueue.</param>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask EnqueueAsync(CallbackRequest request, CancellationToken ct = default)
        => await _queue.Channel.Writer.WriteAsync(request, ct);
}
