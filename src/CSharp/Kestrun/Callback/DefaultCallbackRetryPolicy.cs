namespace Kestrun.Callback;
/// <summary>
/// Default implementation of <see cref="ICallbackRetryPolicy"/> using exponential backoff with jitter.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DefaultCallbackRetryPolicy"/> class.
/// </remarks>

public sealed class DefaultCallbackRetryPolicy : ICallbackRetryPolicy
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly Random _rng = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCallbackRetryPolicy"/> class with the specified options.
    /// </summary>
    /// <param name="options"> The options to configure the retry policy. </param>
    public DefaultCallbackRetryPolicy(CallbackDispatchOptions? options)
    {
        if (options == null)
        {
            _maxAttempts = 3;
            _baseDelay = TimeSpan.FromSeconds(2);
            _maxDelay = TimeSpan.FromSeconds(30);
            return;
        }
        _maxAttempts = options.MaxAttempts;
        _baseDelay = options.BaseDelay;
        _maxDelay = options.MaxDelay;
    }


    /// <summary>
    /// Evaluates the given callback request and result to determine the retry decision.
    /// </summary>
    /// <param name="req"> The callback request to evaluate. </param>
    /// <param name="result"> The result of the callback request. </param>
    /// <returns> A decision indicating whether to retry or stop. </returns>
    public RetryDecision Evaluate(CallbackRequest req, CallbackResult result)
    {
        var nextAttemptNumber = req.Attempt + 1;

        if (nextAttemptNumber >= _maxAttempts)
        {
            return Stop(req, "MaxAttemptsReached");
        }

        if (IsPermanentFailure(result))
        {
            return Stop(req, "PermanentFailure");
        }

        if (!IsRetryable(result))
        {
            return Stop(req, "NotRetryable");
        }

        var delay = ComputeBackoff(nextAttemptNumber);
        var next = DateTimeOffset.UtcNow.Add(delay);

        // Respect Retry-After if you capture it (nice-to-have)
        // if (result.RetryAfter != null) { ... }

        return new RetryDecision(RetryDecisionKind.Retry, next, delay, "RetryableFailure");
    }
    /// <summary>
    /// Creates a Stop retry decision with the given reason.
    /// </summary>
    /// <param name="req"> The callback request for which to create the stop decision. </param>
    /// <param name="reason"> The reason for stopping retries. </param>
    /// <returns>A RetryDecision indicating to stop retries.</returns>
    private static RetryDecision Stop(CallbackRequest req, string reason)
        => new(RetryDecisionKind.Stop, req.NextAttemptAt, TimeSpan.Zero, reason);

    /// <summary>
    /// Determines if the callback result indicates a permanent failure.
    /// </summary>
    /// <param name="r">The callback result to evaluate.</param>
    /// <returns>True if the result indicates a permanent failure; otherwise, false.</returns>
    private static bool IsPermanentFailure(CallbackResult r) =>
        // could treat repeated 401/403 as permanent immediately
        r.StatusCode is 400 or 401 or 403 or 404 or 409 or 422;

    /// <summary>
    /// Determines if the callback result indicates a retryable failure.
    /// </summary>
    /// <param name="r">The callback result to evaluate.</param>
    /// <returns>True if the result indicates a retryable failure; otherwise, false.</returns>
    private static bool IsRetryable(CallbackResult r)
    {
        if (r.ErrorType is "Timeout" or "HttpRequestException")
        {
            return true;
        }

        if (r.StatusCode is null)
        {
            return false;
        }

        var sc = r.StatusCode.Value;
        return sc is 408 or 429 or (>= 500 and <= 599);
    }
    /// <summary>
    /// Computes the exponential backoff delay with jitter based on the attempt number.
    /// </summary>
    /// <param name="attemptNumber">The current attempt number (1-based).</param>
    /// <returns>A TimeSpan representing the delay before the next retry.</returns>
    private TimeSpan ComputeBackoff(int attemptNumber)
    {
        // attemptNumber: 1..N
        var exp = Math.Pow(2, attemptNumber - 1);
        var raw = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * exp);

        var capped = raw <= _maxDelay ? raw : _maxDelay;
        var jitter = TimeSpan.FromMilliseconds(_rng.Next(0, 250));
        return capped + jitter;
    }
}
