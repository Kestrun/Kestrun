namespace Kestrun.Callback;
/// <summary>
/// Dispatcher for enqueuing callback requests.
/// </summary>
public interface ICallbackDispatcher
{
    /// <summary>
    /// Enqueue a callback request for processing.
    /// </summary>
    /// <param name="request">The callback request to enqueue.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask EnqueueAsync(CallbackRequest request, CancellationToken ct = default);
}
