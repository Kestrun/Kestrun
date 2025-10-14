namespace Kestrun.Tasks;

/// <summary>
/// Represents the lifecycle state of a KestrunTask.
/// </summary>
public enum TaskState
{
    /// <summary>
    /// Task has been created but not yet started.
    /// </summary>
    NotStarted,
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
    Failed,
    /// <summary>
    /// Task was stopped.
    /// </summary>
    Stopped,
    /// <summary>
    /// Task is in the process of stopping.
    /// </summary>
    Stopping
}
