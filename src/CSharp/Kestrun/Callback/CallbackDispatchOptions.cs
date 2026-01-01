namespace Kestrun.Callback;
/// <summary>
/// Options for dispatching callback requests.
/// </summary>
public sealed record CallbackDispatchOptions
{
    /// <summary>
    /// Default timeout for callback requests.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of retry attempts for failed callback requests.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;
    /// <summary>
    /// Base delay between retry attempts.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(2);
    /// <summary>
    /// Maximum delay between retry attempts.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
}
