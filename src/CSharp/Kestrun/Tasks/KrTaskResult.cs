namespace Kestrun.Tasks;

/// <summary>
/// Result information for a completed task execution.
/// </summary>
/// <param name="Id">Unique task identifier.</param>
/// <param name="State">Final state of the task when the result was captured.</param>
/// <param name="StartedAt">UTC timestamp when execution started.</param>
/// <param name="CompletedAt">UTC timestamp when execution ended.</param>
/// <param name="Output">Primary output object (C#/VB returns last expression; PowerShell returns array of pipeline output).</param>
/// <param name="Error">Fault exception message or error record text if any.</param>
public sealed record KrTaskResult(
    string Id,
    TaskState State,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    object? Output,
    string? Error
) : KrTask(Id, State, StartedAt, CompletedAt, Error)
{
    /// <summary>
    /// Duration of the execution, if both timestamps are available.
    /// </summary>
    public TimeSpan? Duration => (StartedAt.HasValue && CompletedAt.HasValue)
        ? CompletedAt.Value - StartedAt.Value
        : null;
}
