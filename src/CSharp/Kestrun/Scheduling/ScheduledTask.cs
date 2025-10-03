using Cronos;

namespace Kestrun.Scheduling;

/// <summary>
/// Represents a scheduled task with its configuration and state.
/// This record is used to encapsulate the details of a scheduled task,
/// </summary>
/// <param name="Name">The name of the task.</param>
/// <param name="Work">The work to be performed by the task.</param>
/// <param name="Cron">The cron expression for the task.</param>
/// <param name="Interval">The interval for the task.</param>
/// <param name="RunImmediately">Whether to run the task immediately.</param>
/// <param name="TokenSource">The cancellation token source for the task.</param>
internal sealed record ScheduledTask(
    string Name,
    Func<CancellationToken, Task> Work,
    CronExpression? Cron,
    TimeSpan? Interval,
    bool RunImmediately,
    CancellationTokenSource TokenSource
)
{
    /// <summary>
    /// Lock object for synchronizing timestamp updates to prevent reading inconsistent state
    /// where LastRunAt is newer than NextRunAt.
    /// </summary>
    private readonly object _timestampLock = new();

    private DateTimeOffset? _lastRunAt;
    private DateTimeOffset _nextRunAt;

    /// <summary>
    /// The last time this task was run, or null if it has not run yet.
    /// This is used to determine if the task has run at least once.
    /// </summary>
    public DateTimeOffset? LastRunAt
    {
        get { lock (_timestampLock) { return _lastRunAt; } }
        set { lock (_timestampLock) { _lastRunAt = value; } }
    }

    /// <summary>
    ///  The next time this task is scheduled to run, based on the cron expression or interval.
    ///  If the task is not scheduled, this will be DateTimeOffset.MinValue.
    /// </summary>
    public DateTimeOffset NextRunAt
    {
        get { lock (_timestampLock) { return _nextRunAt; } }
        set { lock (_timestampLock) { _nextRunAt = value; } }
    }

    /// <summary>
    /// Gets both timestamps atomically to prevent reading inconsistent state.
    /// </summary>
    /// <returns>A tuple containing LastRunAt and NextRunAt.</returns>
    public (DateTimeOffset? LastRunAt, DateTimeOffset NextRunAt) GetTimestamps()
    {
        lock (_timestampLock)
        {
            return (_lastRunAt, _nextRunAt);
        }
    }

    /// <summary>
    /// Sets both timestamps atomically to ensure consistent state.
    /// </summary>
    /// <param name="lastRunAt">The last run timestamp.</param>
    /// <param name="nextRunAt">The next run timestamp.</param>
    public void SetTimestamps(DateTimeOffset lastRunAt, DateTimeOffset nextRunAt)
    {
        lock (_timestampLock)
        {
            _lastRunAt = lastRunAt;
            _nextRunAt = nextRunAt;
        }
    }

    /// <summary>
    /// Indicates whether the task is currently suspended.
    /// A suspended task will not run until resumed.
    /// </summary>
    public volatile bool IsSuspended;

    /// <summary>
    /// The background runner task handling the scheduling loop. Used to allow
    /// graceful cancellation (tests assert no further executions after Cancel()).
    /// </summary>
    public Task? Runner { get; set; }

    /// <summary>
    /// Fixed anchor timestamp captured at schedule time for interval jobs to enable fixed-rate scheduling.
    /// </summary>
    public DateTimeOffset AnchorAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Number of successful executions completed (for interval jobs) to compute deterministic next slot.
    /// </summary>
    public int RunIteration { get; set; }

    /// <summary>
    /// True when the scheduling loop has exited.
    /// </summary>
    public volatile bool IsCompleted;
}
