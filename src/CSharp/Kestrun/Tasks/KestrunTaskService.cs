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

        var cfg = new TaskJobFactory.TaskJobConfig(ScriptCode, _log, _pool);
        var work = TaskJobFactory.Create(cfg);
        var cts = new CancellationTokenSource();
        var task = new KestrunTask(id, ScriptCode, cts)
        {
            Work = work
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
        if (task.State != TaskState.Created || task.Runner != null)
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

        if (task.State != TaskState.Created || task.Runner != null)
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


    /// <summary>Gets the current state for a task.</summary>
    public TaskState? GetState(string id)
        => _tasks.TryGetValue(id, out var t) ? t.State : null;

    /// <summary>Gets a snapshot result for a task (works even while running; Output is only set after completion).</summary>
    public TaskResult? GetResult(string id)
        => _tasks.TryGetValue(id, out var t) ? t.ToResult() : null;

    /// <summary>Attempts to cancel a running task.</summary>
    public bool Cancel(string id)
    {
        if (!_tasks.TryGetValue(id, out var t))
        {
            return false;
        }
        if (t.State is TaskState.Completed or TaskState.Faulted or TaskState.Cancelled)
        {
            return false;
        }
        _log.Information("Cancelling task {Id}", id);
        t.TokenSource.Cancel();
        return true;
    }

    /// <summary>Removes a finished task from the registry.</summary>
    public bool Remove(string id) => _tasks.TryRemove(id, out _);

    /// <summary>Lists all tasks with basic info.</summary>
    public IReadOnlyCollection<TaskResult> List()
        => [.. _tasks.Values.Select(v => v.ToResult())];

    private async Task ExecuteAsync(KestrunTask task)
    {
        task.State = TaskState.Running;
        task.StartedAtUtc = DateTimeOffset.UtcNow;
        try
        {
            var result = await task.Work(task.TokenSource.Token).ConfigureAwait(false);
            task.Output = result;
            task.State = task.TokenSource.IsCancellationRequested ? TaskState.Cancelled : TaskState.Completed;
        }
        catch (OperationCanceledException) when (task.TokenSource.IsCancellationRequested)
        {
            task.State = TaskState.Cancelled;
        }
        catch (Exception ex)
        {
            task.Fault = ex;
            task.State = TaskState.Faulted;
            _log.Error(ex, "Task {Id} failed", task.Id);
        }
        finally
        {
            task.CompletedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    //
}
