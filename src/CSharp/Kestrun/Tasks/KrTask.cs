namespace Kestrun.Tasks;

/// <summary>
/// Basic information about a task for listing purposes.
/// </summary>
/// <param name="Id">Unique task identifier.</param>
/// <param name="State">Final state of the task when the result was captured.</param>
/// <param name="StartedAt">UTC timestamp when execution started.</param>
/// <param name="CompletedAt">UTC timestamp when execution ended.</param>
/// <param name="Error">Fault exception message or error record text if any.</param>
public record KrTask(
    string Id,
    TaskState State,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error
)
{
    /// <summary>
    /// The textual name of <see cref="State"/> for display and serialization convenience.
    /// </summary>
    public string StateText => State.ToString();

    /// <summary>
    /// Duration of the execution, if both timestamps are available.
    /// </summary>
    public TimeSpan? Duration => (StartedAt.HasValue && CompletedAt.HasValue)
        ? CompletedAt.Value - StartedAt.Value
        : null;
}
