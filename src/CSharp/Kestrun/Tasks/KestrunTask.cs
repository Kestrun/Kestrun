using Kestrun.Hosting.Options;

namespace Kestrun.Tasks;

/// <summary>
/// Represents a single-shot task execution with its current state and telemetry.
/// </summary>
/// <param name="Id">Unique task identifier.</param>
/// <param name="ScriptCode">The scripting language and code configuration for this task.</param>
/// <param name="tokenSource">Cancellation token source for this task.</param>
public sealed class KestrunTask(string Id, LanguageOptions ScriptCode, CancellationTokenSource tokenSource)
{
    /// <summary>
    /// Unique task identifier.
    /// </summary>
    public string Id { get; init; } = Id;

    /// <summary>
    /// Optional human-friendly name of the task.
    /// </summary>
    public string Name { get; set; } = "Task " + Id;

    /// <summary>
    /// Optional description of the task.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// The scripting language and code configuration for this task.
    /// </summary>
    public LanguageOptions ScriptCode { get; init; } = ScriptCode;
    /// <summary>
    /// Compiled work delegate producing a result.
    /// </summary>
    public required Func<CancellationToken, Task<object?>> Work { get; init; }
    /// <summary>
    /// Cancellation token source for this task.
    /// </summary>
    public CancellationTokenSource TokenSource { get; } = tokenSource;

    /// <summary>
    /// Current state of the task.
    /// </summary>
    public TaskState State { get; internal set; } = TaskState.NotStarted;
    /// <summary>
    /// Fault exception if the task failed.
    /// </summary>
    public Exception? Fault { get; internal set; }
    /// <summary>
    /// Output produced by the task (last expression for C#/VB, pipeline for PowerShell).
    /// </summary>
    public object? Output { get; internal set; }

    /// <summary>
    /// UTC timestamp when execution started.
    /// </summary>
    public DateTimeOffset? StartedAtUtc { get; internal set; }
    /// <summary>
    /// UTC timestamp when execution ended.
    /// </summary>
    public DateTimeOffset? CompletedAtUtc { get; internal set; }

    /// <summary>
    /// The background task executing this work.
    /// </summary>
    public Task? Runner { get; internal set; }

    /// <summary>
    /// Creates a basic task info record for this task.
    /// </summary>
    /// <returns> A basic task info record. </returns>
    public KrTask ToKrTask() => new(Id, Name, Description, State, StartedAtUtc, CompletedAtUtc, Progress,
        Parent?.Id ?? string.Empty,
        [.. Children.Select(c => c.Id)]);

    /// <summary>
    /// Indicates whether the task has reached a terminal state.
    /// </summary>
    public bool Finished => State is TaskState.Completed or TaskState.Failed or TaskState.Stopped;

    /// <summary>
    /// For hierarchical tasks; null for root tasks.
    /// </summary>
    public KestrunTask Parent { get; set; } = null!; // For hierarchical tasks; null for root tasks

    /// <summary>
    /// Child tasks spawned by this task.
    /// </summary>
    public List<KestrunTask> Children { get; } = [];

    /// <summary>
    /// Progress state of the task, if supported by the script.
    /// </summary>
    public ProgressiveKestrunTaskState Progress { get; init; } = new();
}
