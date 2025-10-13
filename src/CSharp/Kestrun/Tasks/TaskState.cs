namespace Kestrun.Tasks;

/// <summary>
/// Represents the lifecycle state of a KestrunTask.
/// </summary>
public enum TaskState
{
    /// <summary>
    /// Task has been created but not yet started.
    /// </summary>
    Created,
    /// <summary>
    /// Task is currently running.
    /// </summary>
    Running,
    /// <summary>
    /// Task has completed successfully.
    /// </summary>
    Completed,
    /// <summary>
    /// Task has failed with an error.
    /// </summary>
    Faulted,
    /// <summary>
    /// Task was cancelled.
    /// </summary>
    Cancelled
}
