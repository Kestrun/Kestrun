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
    /// The last time this task was run, or null if it has not run yet.
    /// This is used to determine if the task has run at least once.
    /// </summary>
    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>
    ///  The next time this task is scheduled to run, based on the cron expression or interval.
    ///  If the task is not scheduled, this will be DateTimeOffset.MinValue.
    /// </summary>
    public DateTimeOffset NextRunAt { get; set; }

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
