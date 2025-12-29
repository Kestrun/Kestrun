namespace Kestrun.Callback;

/// <summary>
/// Enumerates the kinds of retry decisions.
/// </summary>
public enum RetryDecisionKind
{
    /// <summary>
    /// Indicates that the callback should be retried.
    /// </summary>
    Retry,
    /// <summary>
    /// Indicates that no further retries should be attempted.
    /// </summary>
    Stop
}
