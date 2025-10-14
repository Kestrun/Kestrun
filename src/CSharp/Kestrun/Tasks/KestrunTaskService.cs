using System.Collections.Concurrent;
using System.Reflection;
using Kestrun.Hosting.Options;
using Kestrun.Scripting;

namespace Kestrun.Tasks;

/// <summary>
/// Service to run ad-hoc Kestrun tasks in PowerShell, C#, or VB.NET, with status, result, and cancellation.
/// </summary>
/// <summary>
/// Creates a new instance of the KestrunTaskService.
/// </summary>
/// <param name="pool">PowerShell runspace pool manager.</param>
/// <param name="log">Logger instance.</param>
public sealed class KestrunTaskService(KestrunRunspacePoolManager pool, Serilog.ILogger log)
{
    private readonly ConcurrentDictionary<string, KestrunTask> _tasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly KestrunRunspacePoolManager _pool = pool;
    private readonly Serilog.ILogger _log = log;

    /// <summary>
    /// Creates a task from a code snippet without starting it.
    /// </summary>
    public string Create(string? id, LanguageOptions ScriptCode)
    {
        ArgumentNullException.ThrowIfNull(ScriptCode);

        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("n");
        }
        if (_tasks.ContainsKey(id))
        {
            throw new InvalidOperationException($"Task id '{id}' already exists.");
        }

        var progress = new ProgressiveKestrunTaskState();
        var cfg = new TaskJobFactory.TaskJobConfig(ScriptCode, _log, _pool, progress);
        var work = TaskJobFactory.Create(cfg);
        var cts = new CancellationTokenSource();
        var task = new KestrunTask(id, ScriptCode, cts)
        {
            Work = work,
            Progress = progress
        };

        return !_tasks.TryAdd(id, task) ? throw new InvalidOperationException($"Task id '{id}' already exists.") : id;
    }

    /// <summary>Starts a previously created task by id.</summary>
    public bool Start(string id)
    {
        if (!_tasks.TryGetValue(id, out var task))
        {
            return false;
        }
        if (task.State != TaskState.NotStarted || task.Runner != null)
        {
            return false; // only start once from Created state
        }
        task.Runner = Task.Run(async () => await ExecuteAsync(task).ConfigureAwait(false), task.TokenSource.Token);
        return true;
    }

    /// <summary>
    /// Starts a previously created task by id, and awaits its completion.
    /// </summary>
    /// <param name="id">The task identifier.</param>
    /// <returns>True if the task was found and started; false if not found or already started.</returns>
    public async Task<bool> StartAsync(string id)
    {
        if (!_tasks.TryGetValue(id, out var task))
        {
            return false;
        }

        if (task.State != TaskState.NotStarted || task.Runner != null)
        {
            return false; // only start once from Created state
        }

        // Launch the task asynchronously and store its runner
        task.Runner = Task.Run(() => ExecuteAsync(task), task.TokenSource.Token);

        try
        {
            await task.Runner.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Optional: handle cancellation gracefully
        }
        catch (Exception ex)
        {
            // Optional: handle or log errors
            Console.Error.WriteLine($"Task {id} failed: {ex}");
        }

        return true;
    }

    /// <summary>
    /// Gets a task by id.
    /// </summary>
    /// <param name="id">The task identifier.</param>
    /// <returns>The task result, or null if not found.</returns>
    public KrTask? Get(string id)
           => _tasks.TryGetValue(id, out var t) ? t.ToKrTask() : null;

    /// <summary>
    /// Gets the current state for a task.
    /// </summary>
    /// <param name="id">The task identifier.</param>
    /// <returns>The task state, or null if not found.</returns>
    public TaskState? GetState(string id)
        => _tasks.TryGetValue(id, out var t) ? t.State : null;

    /// <summary>
    /// Gets the output object for a completed task.
    /// </summary>
    /// <param name="id">The task identifier.</param>
    /// <returns>The task output object, or null if not found or no output.</returns>
    //   public KrTaskResult? GetResult(string id)
    //     => _tasks.TryGetValue(id, out var t) ? t.ToKrTaskResult() : null;


    public object? GetResult(string id)
           => _tasks.TryGetValue(id, out var t) ? t.Output : null;

    /// <summary>Attempts to cancel a running task.</summary>
    public bool Cancel(string id)
    {
        if (!_tasks.TryGetValue(id, out var t))
        {
            return false;
        }
        if (t.State is TaskState.Completed or TaskState.Failed or TaskState.Stopped)
        {
            return false;
        }
        _log.Information("Cancelling task {Id}", id);
        t.TokenSource.Cancel();
        return true;
    }

    /// <summary>
    /// Checks recursively if all children of a task are finished.
    /// </summary>
    /// <param name="task">The parent task to check.</param>
    /// <returns>True if all children are finished; false otherwise.</returns>
    private static bool ChildrenAreFinished(KestrunTask task)
    {
        foreach (var child in task.Children)
        {
            if (!ChildrenAreFinished(child))
            {
                return false;
            }
            if (child.State is not TaskState.Completed and not TaskState.Failed and not TaskState.Stopped)
            {
                return false;
            }
        }
        return true;
    }
    /// <summary>
    /// Removes a finished task from the registry.
    /// </summary>
    /// <param name="id">The task identifier.</param>
    /// <returns>True if the task was found and removed; false if not found or not finished.</returns>
    /// <remarks>
    /// A task can only be removed if it is in a terminal state (Completed, Failed, Stopped)
    /// and all its child tasks are also in terminal states.
    /// </remarks>
    public bool Remove(string id)
    {
        if (_tasks.TryGetValue(id, out var t))
        {
            if (t.State is TaskState.Completed or TaskState.Failed or TaskState.Stopped)
            {
                if (!ChildrenAreFinished(t))
                {
                    _log.Warning("Cannot remove task {Id} because it has running child tasks", id);
                    return false;
                }

                _log.Information("Removing task {Id}", id);
                if (_tasks.TryRemove(id, out _))
                {
                    if (t.Parent is not null)
                    {
                        _ = t.Parent.Children.Remove(t);
                    }
                    if (t.Children.Count > 0)
                    {
                        foreach (var child in t.Children)
                        {
                            if (!Remove(child.Id))
                            {
                                _log.Warning("Failed to remove child task {ChildId} of parent task {ParentId}", child.Id, id);
                            }
                        }
                    }
                    return true;
                }
            }
            return false;
        }
        return false;
    }

    /// <summary>
    /// Lists all tasks with basic info.
    /// Does not include output or error details.
    /// </summary>
    public IReadOnlyCollection<KrTask> List()
        => [.. _tasks.Values.Select(v => v.ToKrTask())];

    /// <summary>
    /// Executes the task's work function and updates its state accordingly.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ExecuteAsync(KestrunTask task)
    {
        task.State = TaskState.Running;
        task.Progress.StatusMessage = "Running";
        task.StartedAtUtc = DateTimeOffset.UtcNow;
        try
        {
            var result = await task.Work(task.TokenSource.Token).ConfigureAwait(false);
            task.Output = result;
            task.State = task.TokenSource.IsCancellationRequested ? TaskState.Stopped : TaskState.Completed;
            if (task.State == TaskState.Completed)
            {
                task.Progress.Complete("Completed");
            }
        }
        catch (OperationCanceledException) when (task.TokenSource.IsCancellationRequested)
        {
            task.State = TaskState.Stopped;
            task.Progress.Cancel("Cancelled");
        }
        catch (Exception ex)
        {
            task.Fault = ex;
            task.State = TaskState.Failed;
            _log.Error(ex, "Task {Id} failed", task.Id);
            task.Progress.Fail("Failed");
        }
        finally
        {
            task.CompletedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}
