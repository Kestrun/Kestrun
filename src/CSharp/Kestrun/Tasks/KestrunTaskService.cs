using System.Collections.Concurrent;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.Scripting;

namespace Kestrun.Tasks;

/// <summary>
/// Service to run ad-hoc Kestrun tasks in PowerShell, C#, or VB.NET, with status, result, and cancellation.
/// </summary>
/// <param name="pool">PowerShell runspace pool manager.</param>
/// <param name="log">Logger instance.</param>
public sealed class KestrunTaskService(KestrunRunspacePoolManager pool, Serilog.ILogger log) : IDisposable
{
    private static readonly TimeSpan DisposeWaitTimeout = TimeSpan.FromSeconds(5);
    private readonly ConcurrentDictionary<string, KestrunTask> _tasks = new(StringComparer.OrdinalIgnoreCase);
    internal KestrunRunspacePoolManager TaskRunspacePool { get; } = pool;
    private readonly Serilog.ILogger _log = log;
    private int _disposed;

    private KestrunHost Host => TaskRunspacePool.Host;

    /// <summary>
    /// Creates a task from a code snippet without starting it.
    /// </summary>
    /// <param name="id">Optional unique task identifier. If null or empty, a new GUID will be generated.</param>
    /// <param name="scriptCode">The scripting language and code configuration for this task.</param>
    /// <param name="autoStart">Whether to start the task automatically.</param>
    /// <param name="name">Optional human-friendly name of the task.</param>
    /// <param name="description">Optional description of the task.</param>
    /// <returns>The unique identifier of the created task.</returns>
    /// <exception cref="ArgumentNullException">Thrown if scriptCode is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if a task with the same id already exists.</exception>
    public string Create(string? id, LanguageOptions scriptCode, bool autoStart, string? name, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(scriptCode);

        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("n");
        }
        if (_tasks.ContainsKey(id))
        {
            throw new InvalidOperationException($"Task id '{id}' already exists.");
        }

        var progress = new ProgressiveKestrunTaskState();
        var cfg = new TaskJobFactory.TaskJobConfig(Host, id, scriptCode, TaskRunspacePool, progress);
        var work = TaskJobFactory.Create(cfg);
        var cts = new CancellationTokenSource();
        var task = new KestrunTask(id, scriptCode, cts)
        {
            Work = work,
            Progress = progress,
            Name = string.IsNullOrWhiteSpace(name) ? ("Task " + id) : name,
            Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description
        };

        if (!_tasks.TryAdd(id, task))
        {
            throw new InvalidOperationException($"Task id '{id}' already exists.");
        }
        if (autoStart)
        {
            _ = Start(id);
        }
        return id;
    }

    /// <summary>
    /// Sets or updates the name of a task.
    /// </summary>
    /// <param name="id">The task identifier.</param>
    /// <param name="name">The new name for the task.</param>
    /// <returns>True if the task was found and updated; false if not found.</returns>
    public bool SetTaskName(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (!_tasks.TryGetValue(id, out var task))
        {
            return false;
        }
        task.Name = name;
        return true;
    }

    /// <summary>
    /// Sets or updates the description of a task.
    /// </summary>
    /// <param name="id">The task identifier.</param>
    /// <param name="description">The new description for the task.</param>
    /// <returns>True if the task was found and updated; false if not found.</returns>
    public bool SetTaskDescription(string id, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentNullException(nameof(description));
        }
        if (!_tasks.TryGetValue(id, out var task))
        {
            return false;
        }
        task.Description = description;
        return true;
    }

    /// <summary>
    /// Starts a previously created task by id.
    /// </summary>
    /// <param name="id">The task identifier.</param>
    /// <returns>True if the task was found and started; false if not found or already started.</returns>
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
        task.Runner = Task.Run(async () => await ExecuteAsync(task).ConfigureAwait(false), task.Token);
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
        task.Runner = Task.Run(() => ExecuteAsync(task), task.Token);

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
            _log.Error(ex, "Task {Id} failed", id);
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
    public object? GetResult(string id)
           => _tasks.TryGetValue(id, out var t) ? t.Output : null;

    /// <summary>
    /// Attempts to cancel a task.
    /// </summary>
    /// <remarks>
    /// If the task has not been started (NotStarted) it is transitioned directly to the terminal
    /// Stopped state so it can be removed later and does not remain orphaned.
    /// </remarks>
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

        if (t.State == TaskState.NotStarted)
        {
            _log.Information("Cancelling task {Id} before start", id);
            // Transition to a terminal state so Remove() can succeed
            t.State = TaskState.Stopped;
            t.Progress.Cancel("Cancelled before start");
            var now = DateTimeOffset.UtcNow;
            t.StartedAtUtc ??= now;
            t.CompletedAtUtc = now;
            t.TokenSource.Cancel(); // ensure any future Start() attempt observes cancellation
            return true;
        }

        _log.Information("Cancelling running task {Id}", id);
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
                    // Detach from parent first so recursive child removals can't mutate the list we iterate
                    if (t.Parent is not null)
                    {
                        _ = t.Parent.Children.Remove(t);
                    }

                    // Take a point-in-time snapshot because recursive Remove(child.Id) will
                    // mutate the parent's Children collection (each child removes itself).
                    if (t.Children.Count > 0)
                    {
                        var snapshot = t.Children.ToArray();
                        foreach (var child in snapshot)
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
    /// Tries to get the live task model for internal consumers such as tests.
    /// </summary>
    /// <param name="id">The task identifier.</param>
    /// <param name="task">The matching task when found; otherwise null.</param>
    /// <returns>True when the task exists; otherwise false.</returns>
    internal bool TryGetTask(string id, out KestrunTask? task)
        => _tasks.TryGetValue(id, out task);

    /// <summary>
    /// Executes the task's work function and updates its state accordingly.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ExecuteAsync(KestrunTask task)
    {
        var cancellationToken = task.Token;
        task.State = TaskState.Running;
        task.Progress.StatusMessage = "Running";
        task.StartedAtUtc = DateTimeOffset.UtcNow;
        try
        {
            var result = await task.Work(cancellationToken).ConfigureAwait(false);
            task.Output = result;
            task.State = cancellationToken.IsCancellationRequested ? TaskState.Stopped : TaskState.Completed;
            if (task.State == TaskState.Completed)
            {
                task.Progress.Complete("Completed");
            }
            else if (task.State == TaskState.Stopped)
            {
                // If cancellation was requested but no exception was thrown (graceful exit), normalize progress
                task.Progress.Cancel("Cancelled");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            task.State = TaskState.Stopped;
            task.Progress.Cancel("Cancelled");
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Some libraries throw TaskCanceledException instead of OperationCanceledException on cancellation
            task.State = TaskState.Stopped;
            task.Progress.Cancel("Cancelled");
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            // During cancellation, certain engines (e.g., PowerShell) may surface non-cancellation exceptions
            // such as PipelineStoppedException. If cancellation was requested, normalize to Stopped.
            task.State = TaskState.Stopped;
            task.Progress.Cancel("Cancelled");
            _log.Information(ex, "Task {Id} cancelled with exception after cancellation was requested", task.Id);
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

    /// <summary>
    /// Cancels active tasks, waits briefly for runners to quiesce, disposes quiesced cancellation sources,
    /// clears the task registry, and releases the task runspace pool.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var tasks = _tasks.Values.ToArray();
        CancelTasks(tasks);
        WaitForRunnersToQuiesce(tasks);
        DisposeQuiescedCancellationSources(tasks);

        _tasks.Clear();
        TaskRunspacePool.Dispose();
        _log.Information("KestrunTaskService disposed");
    }

    /// <summary>
    /// Requests cancellation for each tracked task during service shutdown.
    /// </summary>
    /// <param name="tasks">The tasks being shut down.</param>
    private void CancelTasks(IReadOnlyCollection<KestrunTask> tasks)
    {
        foreach (var task in tasks)
        {
            try
            {
                task.TokenSource.Cancel();
            }
            catch (Exception ex)
            {
                _log.Debug(ex, "Failed to cancel task {Id} during KestrunTaskService disposal", task.Id);
            }
        }
    }

    /// <summary>
    /// Waits for active task runners to reach a terminal state within the shutdown timeout.
    /// </summary>
    /// <param name="tasks">The tasks being shut down.</param>
    private void WaitForRunnersToQuiesce(IReadOnlyCollection<KestrunTask> tasks)
    {
        var allRunnersCompleted = SpinWait.SpinUntil(
            () => tasks.All(task => task.Runner is null || task.Runner.IsCompleted),
            DisposeWaitTimeout);

        if (!allRunnersCompleted)
        {
            var activeCount = tasks.Count(task => task.Runner is { IsCompleted: false });
            _log.Debug(
                "Timed out waiting for {ActiveCount} task runner(s) to stop during KestrunTaskService disposal after {TimeoutMs} ms",
                activeCount,
                DisposeWaitTimeout.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Disposes cancellation token sources for tasks that are no longer executing.
    /// </summary>
    /// <param name="tasks">The tasks being shut down.</param>
    private void DisposeQuiescedCancellationSources(IReadOnlyCollection<KestrunTask> tasks)
    {
        foreach (var task in tasks)
        {
            if (task.Runner is { IsCompleted: false })
            {
                _log.Debug("Skipping CancellationTokenSource disposal for still-running task {Id}", task.Id);
                continue;
            }

            try
            {
                task.TokenSource.Dispose();
            }
            catch (Exception ex)
            {
                _log.Debug(ex, "Failed to dispose CancellationTokenSource for task {Id} during KestrunTaskService disposal", task.Id);
            }
        }
    }
}
