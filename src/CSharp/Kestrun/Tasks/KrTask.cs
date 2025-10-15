namespace Kestrun.Tasks;

/// <summary>
/// Basic information about a task for listing purposes.
/// </summary>
/// <param name="Id">Unique task identifier.</param>
/// <param name="Name">Optional human-friendly name of the task.</param>
/// <param name="Description">Optional description of the task.</param>
/// <param name="State">Final state of the task when the result was captured.</param>
/// <param name="StartedAt">UTC timestamp when execution started.</param>
/// <param name="CompletedAt">UTC timestamp when execution ended.</param>
/// <param name="Progress">Optional progress state of the task.</param>
/// <param name="ParentId">Optional identifier of the parent task, if any.</param>
/// <param name="ChildrenId">Identifiers of any child tasks spawned by this task.</param>
public record KrTask(
    string Id,
    string Name,
    string Description,
    TaskState State,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    ProgressiveKestrunTaskState? Progress,
    string ParentId,
    string[] ChildrenId
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
