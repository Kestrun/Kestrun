namespace Kestrun.Callback;

/// <summary>
/// Represents the result of a callback operation.
/// </summary>
/// <param name="Success">Indicates whether the callback was successful</param>
/// <param name="StatusCode">HTTP status code returned by the callback, if applicable</param>
/// <param name="ErrorType">Type of error encountered, if any (e.g., Timeout, Dns, Tls, Http5xx)</param>
/// <param name="ErrorMessage">Detailed error message, if any</param>
/// <param name="CompletedAt">Timestamp when the callback operation was completed</param>
public sealed record CallbackResult(
    bool Success,
    int? StatusCode,
    string? ErrorType,              // Timeout, Dns, Tls, Http5xx, etc.
    string? ErrorMessage,
    DateTimeOffset CompletedAt
);
