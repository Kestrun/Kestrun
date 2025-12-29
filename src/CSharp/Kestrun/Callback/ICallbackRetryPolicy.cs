namespace Kestrun.Callback;

/// <summary>
/// Defines a policy for retrying callback requests based on their results.
/// </summary>
public interface ICallbackRetryPolicy
{
    /// <summary>
    /// Evaluates the given callback request and result to determine the retry decision.
    /// </summary>
    /// <param name="req">The callback request to evaluate.</param>
    /// <param name="result">The result of the callback request.</param>
    /// <returns>A decision indicating whether to retry or stop.</returns>
    RetryDecision Evaluate(CallbackRequest req, CallbackResult result);
}
