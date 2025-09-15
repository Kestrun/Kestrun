namespace Kestrun.Scheduling;

/// <summary>
/// Represents a scheduled task with its configuration and state.
/// </summary>
/// <param name="Name">The name of the scheduled task.</param>
/// <param name="LastRunAt">The last time the task was run.</param>
/// <param name="NextRunAt">The next scheduled run time for the task.</param>
/// <param name="IsSuspended">Indicates whether the task is currently suspended.</param>
/// <param name="IsCompleted">Indicates whether the scheduling loop has exited (task fully cancelled).</param>
/// <remarks>
/// This class encapsulates the details of a scheduled task, including its name, last run time,
/// next run time, and whether it is currently suspended. It is used internally by the scheduler
/// to manage and report on scheduled tasks.
/// </remarks>
public sealed record JobInfo(string Name,
                             DateTimeOffset? LastRunAt,
                             DateTimeOffset NextRunAt,
                             bool IsSuspended,
                             bool IsCompleted = false);
