namespace Kestrun.Callback;

/// <summary>
/// Represents a decision made by a callback retry policy.
/// </summary>
public sealed record RetryDecision(
    RetryDecisionKind Kind,
    DateTimeOffset NextAttemptAt,
    TimeSpan Delay,
    string Reason
);
