namespace Kestrun.Callback;
/// <summary>
/// Options for dispatching callback requests.
/// </summary>
public sealed class CallbackDispatchOptions
{
    /// <summary>
    /// Default timeout for callback requests.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Optional signature key ID for signing callback requests.
    /// </summary>
    public string? SignatureKeyId { get; set; }

    // optional: you can add MaxAttempts/BaseDelay/MaxDelay later for retries
}
