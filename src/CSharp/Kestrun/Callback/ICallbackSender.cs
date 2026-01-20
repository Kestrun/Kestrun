namespace Kestrun.Callback;

/// <summary>
/// Sender for performing callback requests.
/// </summary>
public interface ICallbackSender
{
    /// <summary>
    /// Sends a callback request asynchronously.
    /// </summary>
    /// <param name="request">The callback request to send.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing the callback result.</returns>
    Task<CallbackResult> SendAsync(CallbackRequest request, CancellationToken ct);
}

