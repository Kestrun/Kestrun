namespace Kestrun.Callback;

/// <summary>
/// Store for persisting callback requests and their states.
/// </summary>
public interface ICallbackStore
{
    /// <summary>
    /// Saves a new callback request.
    /// </summary>
    /// <param name="req">The callback request to save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveNewAsync(CallbackRequest req, CancellationToken ct);

    /// <summary>
    /// Marks the given callback request as in-flight.
    /// </summary>
    /// <param name="req">The callback request to mark as in-flight.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task MarkInFlightAsync(CallbackRequest req, CancellationToken ct);

    /// <summary>
    /// Marks the given callback request as succeeded.
    /// </summary>
    /// <param name="req">The callback request to mark as succeeded.</param>
    /// <param name="res">The result of the callback.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task MarkSucceededAsync(CallbackRequest req, CallbackResult res, CancellationToken ct);

    /// <summary>
    /// Marks the given callback request as retry scheduled.
    /// </summary>
    /// <param name="req">The callback request to mark as retry scheduled.</param>
    /// <param name="res">The result of the callback.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task MarkRetryScheduledAsync(CallbackRequest req, CallbackResult res, CancellationToken ct);

    /// <summary>
    /// Marks the given callback request as failed permanently.
    /// </summary>
    /// <param name="req">The callback request to mark as failed permanently.</param>
    /// <param name="res">The result of the callback.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task MarkFailedPermanentAsync(CallbackRequest req, CallbackResult res, CancellationToken ct);

    /// <summary>
    /// Dequeues due callback requests up to the specified maximum.
    /// </summary>
    /// <param name="max">The maximum number of callback requests to dequeue</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IReadOnlyList<CallbackRequest>> DequeueDueAsync(int max, CancellationToken ct);
}
