using System.Collections.Concurrent;
using Cronos;
using System.Management.Automation;
using System.Collections;
using Kestrun.Utilities;
using static Kestrun.Scheduling.JobFactory;
using Kestrun.Scripting;

namespace Kestrun.Scheduling;

/// <summary>
/// Represents a service for managing scheduled tasks.
/// Provides methods to schedule, cancel, pause, resume, and report on tasks.
/// This service is designed to run within a Kestrun application context.
/// It supports both C# and PowerShell jobs, allowing for flexible scheduling options.
/// </summary>
/// <remarks>
/// The service uses a runspace pool for PowerShell jobs and supports scheduling via cron expressions or intervals.
/// It also provides methods to retrieve task reports in various formats, including typed objects and PowerShell-friendly hashtables.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="SchedulerService"/> class.
/// This constructor sets up the scheduler service with a specified runspace pool, logger, and optional time zone.
/// The runspace pool is used for executing PowerShell scripts, while the logger is used for logging events.
/// </remarks>
/// <param name="pool">The runspace pool manager for executing PowerShell scripts.</param>
/// <param name="log">The logger instance for logging events.</param>
/// <param name="tz">The optional time zone information.</param>
public sealed class SchedulerService(KestrunRunspacePoolManager pool, Serilog.ILogger log, TimeZoneInfo? tz = null) : IDisposable
{
    /// <summary>
    /// The collection of scheduled tasks.
    /// This dictionary maps task names to their corresponding <see cref="ScheduledTask"/> instances.
    /// It is used to manage the lifecycle of scheduled tasks, including scheduling, execution, and cancellation.
    /// It is thread-safe and allows for concurrent access, ensuring that tasks can be added, removed, and executed
    /// simultaneously without causing data corruption or inconsistencies.
    /// </summary>
    private readonly ConcurrentDictionary<string, ScheduledTask> _tasks =
        new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// The runspace pool manager used for executing PowerShell scripts.
    /// This manager is responsible for managing the lifecycle of PowerShell runspaces,
    /// allowing for efficient execution of PowerShell scripts within the scheduler.
    /// It is used to create and manage runspaces for executing scheduled PowerShell jobs.
    /// The pool can be configured with various settings such as maximum runspaces, idle timeout, etc.
    /// </summary>
    private readonly KestrunRunspacePoolManager _pool = pool;
    /// <summary>
    /// The logger instance used for logging events within the scheduler service.
    /// This logger is used to log information, warnings, and errors related to scheduled tasks.
    /// </summary>
    private readonly Serilog.ILogger _log = log;
    /// <summary>
    /// The time zone used for scheduling and reporting.
    /// This is used to convert scheduled times to the appropriate time zone for display and execution.
    /// </summary>
    private readonly TimeZoneInfo _tz = tz ?? TimeZoneInfo.Local;

    /*────────── C# JOBS ──────────*/
    /// <summary>
    /// Schedules a C# job to run at a specified interval.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="interval">The interval between job executions.</param>
    /// <param name="job">The asynchronous job delegate to execute.</param>
    /// <param name="runImmediately">Whether to run the job immediately upon scheduling.</param>
    public void Schedule(string name, TimeSpan interval,
        Func<CancellationToken, Task> job, bool runImmediately = false)
        => ScheduleCore(name, job, cron: null, interval: interval, runImmediately);

    /// <summary>
    /// Schedules a C# job to run according to a cron expression.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="cronExpr">The cron expression specifying the job schedule.</param>
    /// <param name="job">The asynchronous job delegate to execute.</param>
    /// <param name="runImmediately">Whether to run the job immediately upon scheduling.</param>
    public void Schedule(string name, string cronExpr,
        Func<CancellationToken, Task> job, bool runImmediately = false)
    {
        var cron = CronExpression.Parse(cronExpr, CronFormat.IncludeSeconds);
        ScheduleCore(name, job, cron, null, runImmediately);
    }

    /*────────── PowerShell JOBS ──────────*/
    /// <summary>
    /// Schedules a PowerShell job to run according to a cron expression.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="cron">The cron expression specifying the job schedule.</param>
    /// <param name="scriptblock">The PowerShell script block to execute.</param>
    /// <param name="runImmediately">Whether to run the job immediately upon scheduling.</param>
    public void Schedule(string name, string cron, ScriptBlock scriptblock, bool runImmediately = false)
    {
        JobConfig config = new(ScriptLanguage.PowerShell, scriptblock.ToString(), _log, _pool);
        var job = Create(config);
        Schedule(name, cron, job, runImmediately);
    }
    /// <summary>
    /// Schedules a PowerShell job to run at a specified interval.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="interval">The interval between job executions.</param>
    /// <param name="scriptblock">The PowerShell script block to execute.</param>
    /// <param name="runImmediately">Whether to run the job immediately upon scheduling.</param>
    public void Schedule(string name, TimeSpan interval, ScriptBlock scriptblock, bool runImmediately = false)
    {
        JobConfig config = new(ScriptLanguage.PowerShell, scriptblock.ToString(), _log, _pool);
        var job = Create(config);
        Schedule(name, interval, job, runImmediately);
    }
    /// <summary>
    /// Schedules a script job to run at a specified interval.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="interval">The interval between job executions.</param>
    /// <param name="code">The script code to execute.</param>
    /// <param name="lang">The language of the script (e.g., PowerShell, CSharp).</param>
    /// <param name="runImmediately">Whether to run the job immediately upon scheduling.</param>
    public void Schedule(string name, TimeSpan interval, string code, ScriptLanguage lang, bool runImmediately = false)
    {
        JobConfig config = new(lang, code, _log, _pool);
        var job = Create(config);
        Schedule(name, interval, job, runImmediately);
    }

    /// <summary>
    /// Schedules a script job to run according to a cron expression.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="cron">The cron expression specifying the job schedule.</param>
    /// <param name="code">The script code to execute.</param>
    /// <param name="lang">The language of the script (e.g., PowerShell, CSharp).</param>
    /// <param name="runImmediately">Whether to run the job immediately upon scheduling.</param>
    public void Schedule(string name, string cron, string code, ScriptLanguage lang, bool runImmediately = false)
    {
        JobConfig config = new(lang, code, _log, _pool);
        var job = Create(config);
        Schedule(name, cron, job, runImmediately);
    }

    /// <summary>
    /// Schedules a script job from a file to run at a specified interval.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="interval">The interval between job executions.</param>
    /// <param name="fileInfo">The file containing the script code to execute.</param>
    /// <param name="lang">The language of the script (e.g., PowerShell, CSharp).</param>
    /// <param name="runImmediately">Whether to run the job immediately upon scheduling.</param>
    public void Schedule(string name, TimeSpan interval, FileInfo fileInfo, ScriptLanguage lang, bool runImmediately = false)
    {
        JobConfig config = new(lang, string.Empty, _log, _pool);
        var job = Create(config, fileInfo);
        Schedule(name, interval, job, runImmediately);
    }

    /// <summary>
    /// Schedules a script job from a file to run according to a cron expression.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="cron">The cron expression specifying the job schedule.</param>
    /// <param name="fileInfo">The file containing the script code to execute.</param>
    /// <param name="lang">The language of the script (e.g., PowerShell, CSharp).</param>
    /// <param name="runImmediately">Whether to run the job immediately upon scheduling.</param>
    public void Schedule(string name, string cron, FileInfo fileInfo, ScriptLanguage lang, bool runImmediately = false)
    {
        JobConfig config = new(lang, string.Empty, _log, _pool);
        var job = Create(config, fileInfo);
        Schedule(name, cron, job, runImmediately);
    }

    /// <summary>
    /// Asynchronously schedules a script job from a file to run at a specified interval.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="interval">The interval between job executions.</param>
    /// <param name="fileInfo">The file containing the script code to execute.</param>
    /// <param name="lang">The language of the script (e.g., PowerShell, CSharp).</param>
    /// <param name="runImmediately">Whether to run the job immediately upon scheduling.</param>
    /// <param name="ct">The cancellation token to cancel the operation.</param>
    public async Task ScheduleAsync(string name, TimeSpan interval, FileInfo fileInfo, ScriptLanguage lang, bool runImmediately = false, CancellationToken ct = default)
    {
        JobConfig config = new(lang, string.Empty, _log, _pool);
        var job = await CreateAsync(config, fileInfo, ct);
        Schedule(name, interval, job, runImmediately);
    }

    /// <summary>
    /// Asynchronously schedules a script job from a file to run according to a cron expression.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="cron">The cron expression specifying the job schedule.</param>
    /// <param name="fileInfo">The file containing the script code to execute.</param>
    /// <param name="lang">The language of the script (e.g., PowerShell, CSharp).</param>
    /// <param name="runImmediately">Whether to run the job immediately upon scheduling.</param>
    /// <param name="ct">The cancellation token to cancel the operation.</param>
    public async Task ScheduleAsync(string name, string cron, FileInfo fileInfo, ScriptLanguage lang, bool runImmediately = false, CancellationToken ct = default)
    {
        JobConfig config = new(lang, string.Empty, _log, _pool);
        var job = await CreateAsync(config, fileInfo, ct);
        Schedule(name, cron, job, runImmediately);
    }
    /*────────── CONTROL ──────────*/
    /// <summary>
    /// Cancels a scheduled job by its name.
    /// </summary>
    /// <param name="name">The name of the job to cancel.</param>
    /// <returns>True if the job was found and cancelled; otherwise, false.</returns>
    public bool Cancel(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task name cannot be null or empty.", nameof(name));
        }

        _log.Information("Cancelling scheduler job {Name}", name);
        if (_tasks.TryRemove(name, out var task))
        {
            task.TokenSource.Cancel();
            // Wait briefly for the loop to observe cancellation to avoid a race
            // where a final run completes after Cancel() returns and causes test flakiness.
            try
            {
                if (task.Runner is { } r && !r.IsCompleted)
                {
                    // First quick wait
                    if (!r.Wait(TimeSpan.FromMilliseconds(250)))
                    {
                        // Allow additional time (slower net8 CI, PowerShell warm-up) up to ~1s total.
                        var remaining = TimeSpan.FromMilliseconds(750);
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        while (!r.IsCompleted && sw.Elapsed < remaining)
                        {
                            // small sleep; runner work is CPU-light
                            Thread.Sleep(25);
                        }
                    }
                }
            }
            catch (Exception) { /* swallow */ }
            _log.Information("Scheduler job {Name} cancelled", name);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Asynchronously cancels a scheduled job and optionally waits for its runner to complete.
    /// </summary>
    /// <param name="name">Job name.</param>
    /// <param name="timeout">Optional timeout (default 2s) to wait for completion after signalling cancellation.</param>
    public async Task<bool> CancelAsync(string name, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(2);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task name cannot be null or empty.", nameof(name));
        }
        if (!_tasks.TryRemove(name, out var task))
        {
            return false;
        }
        _log.Information("Cancelling scheduler job (async) {Name}", name);
        task.TokenSource.Cancel();
        var runner = task.Runner;
        if (runner is null)
        {
            return true;
        }
        try
        {
            using var cts = new CancellationTokenSource(timeout.Value);
            var completed = await Task.WhenAny(runner, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token)) == runner;
            if (!completed)
            {
                _log.Warning("Timeout waiting for scheduler job {Name} to cancel", name);
            }
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Error while awaiting cancellation for job {Name}", name);
        }
        return true;
    }

    /// <summary>
    /// Cancels all scheduled jobs.
    /// </summary>
    public void CancelAll()
    {
        foreach (var kvp in _tasks.Keys)
        {
            _ = Cancel(kvp);
        }
    }

    /// <summary>
    /// Generates a report of all scheduled jobs, including their last and next run times, and suspension status.
    /// </summary>
    /// <param name="displayTz">The time zone to display times in; defaults to UTC if not specified.</param>
    /// <returns>A <see cref="ScheduleReport"/> containing information about all scheduled jobs.</returns>
    public ScheduleReport GetReport(TimeZoneInfo? displayTz = null)
    {
        // default to Zulu
        var timezone = displayTz ?? TimeZoneInfo.Utc;
        var now = DateTimeOffset.UtcNow;

        var jobs = _tasks.Values
            .Select(t =>
            {
                // store timestamps internally in UTC; convert only for the report
                var last = t.LastRunAt?.ToOffset(timezone.GetUtcOffset(t.LastRunAt.Value));
                var next = t.NextRunAt.ToOffset(timezone.GetUtcOffset(t.NextRunAt));

                return new JobInfo(t.Name, last, next, t.IsSuspended);
            })
            .OrderBy(j => j.NextRunAt)
            .ToArray();

        return new ScheduleReport(now, jobs);
    }

    /// <summary>
    /// Generates a report of all scheduled jobs in a PowerShell-friendly hashtable format.
    /// </summary>
    /// <param name="displayTz">The time zone to display times in; defaults to UTC if not specified.</param>
    /// <returns>A <see cref="Hashtable"/> containing information about all scheduled jobs.</returns>
    public Hashtable GetReportHashtable(TimeZoneInfo? displayTz = null)
    {
        var rpt = GetReport(displayTz);

        var jobsArray = rpt.Jobs
            .Select(j => new Hashtable
            {
                ["Name"] = j.Name,
                ["LastRunAt"] = j.LastRunAt,
                ["NextRunAt"] = j.NextRunAt,
                ["IsSuspended"] = j.IsSuspended,
                ["IsCompleted"] = j.IsCompleted
            })
            .ToArray();                       // powershell likes [] not IList<>

        return new Hashtable
        {
            ["GeneratedAt"] = rpt.GeneratedAt,
            ["Jobs"] = jobsArray
        };
    }


    /// <summary>
    /// Gets a snapshot of all scheduled jobs with their current state.
    /// </summary>
    /// <returns>An <see cref="IReadOnlyCollection{JobInfo}"/> containing job information for all scheduled jobs.</returns>
    public IReadOnlyCollection<JobInfo> GetSnapshot()
        => [.. _tasks.Values.Select(t => new JobInfo(t.Name, t.LastRunAt, t.NextRunAt, t.IsSuspended, t.IsCompleted))];


    /// <summary>
    /// Gets a snapshot of all scheduled jobs with their current state, optionally filtered and formatted.
    /// </summary>
    /// <param name="tz">The time zone to display times in; defaults to UTC if not specified.</param>
    /// <param name="asHashtable">Whether to return the result as PowerShell-friendly hashtables.</param>
    /// <param name="nameFilter">Optional glob patterns to filter job names.</param>
    /// <returns>
    /// An <see cref="IReadOnlyCollection{T}"/> containing job information for all scheduled jobs,
    /// either as <see cref="JobInfo"/> objects or hashtables depending on <paramref name="asHashtable"/>.
    /// </returns>
    public IReadOnlyCollection<object> GetSnapshot(
       TimeZoneInfo? tz = null,
       bool asHashtable = false,
       params string[] nameFilter)
    {
        tz ??= TimeZoneInfo.Utc;

        bool Matches(string name)
        {
            if (nameFilter == null || nameFilter.Length == 0)
            {
                return true;
            }

            foreach (var pat in nameFilter)
            {
                if (RegexUtils.IsGlobMatch(name, pat))
                {
                    return true;
                }
            }

            return false;
        }

        // fast path: no filter, utc, typed objects
        if (nameFilter.Length == 0 && tz.Equals(TimeZoneInfo.Utc) && !asHashtable)
        {
            return [.. _tasks.Values.Select(t => (object)new JobInfo(t.Name, t.LastRunAt, t.NextRunAt, t.IsSuspended, t.IsCompleted))];
        }

        var jobs = _tasks.Values
                         .Where(t => Matches(t.Name))
                         .Select(t =>
                         {
                             var last = t.LastRunAt?.ToOffset(tz.GetUtcOffset(t.LastRunAt ?? DateTimeOffset.UtcNow));
                             var next = t.NextRunAt.ToOffset(tz.GetUtcOffset(t.NextRunAt));
                             return new JobInfo(t.Name, last, next, t.IsSuspended, t.IsCompleted);
                         })
                         .OrderBy(j => j.NextRunAt)
                         .ToArray();

        if (!asHashtable)
        {
            return [.. jobs.Cast<object>()];
        }

        // PowerShell-friendly shape
        return [.. jobs.Select(j => (object)new Hashtable
                {
                    ["Name"]        = j.Name,
                    ["LastRunAt"]   = j.LastRunAt,
                    ["NextRunAt"]   = j.NextRunAt,
                    ["IsSuspended"] = j.IsSuspended,
                    ["IsCompleted"] = j.IsCompleted
                })];
    }


    /// <summary>
    /// Pauses a scheduled job by its name.
    /// </summary>
    /// <param name="name">The name of the job to pause.</param>
    /// <returns>True if the job was found and paused; otherwise, false.</returns>
    public bool Pause(string name) => Suspend(name);
    /// <summary>
    /// Resumes a scheduled job by its name.
    /// </summary>
    /// <param name="name">The name of the job to resume.</param>
    /// <returns>True if the job was found and resumed; otherwise, false.</returns>
    public bool Resume(string name) => Suspend(name, false);

    /*────────── INTERNALS ──────────*/

    /// <summary>
    /// Suspends or resumes a scheduled job by its name.
    /// This method updates the suspension status of the job, allowing it to be paused or resumed.
    /// If the job is found, its IsSuspended property is updated accordingly.
    /// </summary>
    /// <param name="name">The name of the job to suspend or resume.</param>
    /// <param name="suspend">True to suspend the job; false to resume it.</param>
    /// <returns>True if the job was found and its status was updated; otherwise, false.</returns>
    /// <exception cref="ArgumentException"></exception>
    /// <remarks>
    /// This method is used internally to control the execution of scheduled jobs.
    /// It allows for dynamic control over job execution without needing to cancel and reschedule them.
    /// </remarks>
    private bool Suspend(string name, bool suspend = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Task name cannot be null or empty.", nameof(name));
        }

        if (_tasks.TryGetValue(name, out var task))
        {
            task.IsSuspended = suspend;
            _log.Information("Scheduler job {Name} {Action}", name, suspend ? "paused" : "resumed");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Schedules a new job.
    /// This method is the core implementation for scheduling jobs, allowing for both cron-based and interval-based scheduling.
    /// It creates a new <see cref="ScheduledTask"/> instance and starts it in a background loop.
    /// The task is added to the internal collection of tasks, and its next run time is calculated based on the provided cron expression or interval.
    /// If both cron and interval are null, an exception is thrown.
    /// </summary>
    /// <param name="name">The name of the job.</param>
    /// <param name="job">The job to execute.</param>
    /// <param name="cron">The cron expression for scheduling.</param>
    /// <param name="interval">The interval for scheduling.</param>
    /// <param name="runImmediately">Whether to run the job immediately.</param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    /// <remarks>
    /// This method is used internally to schedule jobs and should not be called directly.
    /// It handles the creation of the task, its scheduling, and the management of its execution loop.
    /// The task is run in a separate background thread to avoid blocking the main application flow.
    /// </remarks>
    private void ScheduleCore(
        string name,
        Func<CancellationToken, Task> job,
        CronExpression? cron,
        TimeSpan? interval,
        bool runImmediately)
    {
        if (cron is null && interval == null)
        {
            throw new ArgumentException("Either cron or interval must be supplied.");
        }

        var cts = new CancellationTokenSource();
        var task = new ScheduledTask(name, job, cron, interval, runImmediately, cts)
        {
            NextRunAt = interval != null
                ? DateTimeOffset.UtcNow + interval.Value
                : (DateTimeOffset.UtcNow + NextCronDelay(cron!, _tz)).ToUniversalTime(),
        };

        if (!_tasks.TryAdd(name, task))
        {
            throw new InvalidOperationException($"A task named '{name}' already exists.");
        }

        task.Runner = Task.Run(() => LoopAsync(task), cts.Token);
        _log.Debug("Scheduled job '{Name}' (cron: {Cron}, interval: {Interval})", name, cron?.ToString(), interval);
    }

    /// <summary>
    /// Runs the scheduled task in a loop.
    /// This method handles the execution of the task according to its schedule, either immediately or based on a cron expression or interval.
    /// It checks if the task is suspended and delays accordingly, while also being responsive to cancellation requests.
    /// </summary>
    /// <param name="task">The scheduled task to run.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method is called internally by the scheduler to manage the execution of scheduled tasks.
    /// It ensures that tasks are run at the correct times and handles any exceptions that may occur during execution.
    /// The loop continues until the task is cancelled or the cancellation token is triggered.
    /// </remarks>
    private async Task LoopAsync(ScheduledTask task)
    {
        var ct = task.TokenSource.Token;

        if (task.RunImmediately && !task.IsSuspended)
        {
            await SafeRun(task.Work, task, ct);
        }

        while (!ct.IsCancellationRequested)
        {
            if (task.IsSuspended)
            {
                // sleep a bit while suspended, but stay responsive to Cancel()
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                continue;
            }

            TimeSpan delay;
            if (task.Interval is not null)
            {
                // Align to the intended NextRunAt rather than drifting by fixed interval;
                // this reduces flakiness when scheduling overhead is high.
                var until = task.NextRunAt - DateTimeOffset.UtcNow;
                delay = until > TimeSpan.Zero ? until : TimeSpan.Zero;
            }
            else
            {
                delay = NextCronDelay(task.Cron!, _tz);
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }
            }

            try { await Task.Delay(delay, ct); }
            catch (TaskCanceledException) { break; }

            if (!ct.IsCancellationRequested)
            {
                await SafeRun(task.Work, task, ct);
            }
        }
        task.IsCompleted = true;
    }

    /// <summary>
    /// Calculates the next delay for a cron expression.
    /// This method computes the time until the next occurrence of the cron expression based on the current UTC time.
    /// If there are no future occurrences, it logs a warning and returns a maximum value.
    /// </summary>
    /// <param name="expr">The cron expression to evaluate.</param>
    /// <param name="tz">The time zone to use for the evaluation.</param>
    /// <returns>The time span until the next occurrence of the cron expression.</returns>
    /// <remarks>
    /// This method is used internally to determine when the next scheduled run of a cron-based task should occur.
    /// It uses the Cronos library to calculate the next occurrence based on the current UTC time and the specified time zone.
    /// If no future occurrence is found, it logs a warning and returns a maximum time span.
    /// </remarks>
    private TimeSpan NextCronDelay(CronExpression expr, TimeZoneInfo tz)
    {
        var next = expr.GetNextOccurrence(DateTimeOffset.UtcNow, tz);
        if (next is null)
        {
            _log.Warning("Cron expression {Expr} has no future occurrence", expr);
        }

        return next.HasValue ? next.Value - DateTimeOffset.UtcNow : TimeSpan.MaxValue;
    }

    /// <summary>
    /// Safely runs the scheduled task, handling exceptions and updating the task's state.
    /// This method executes the provided work function and updates the task's last run time and next run time accordingly.
    /// </summary>
    /// <param name="work">The work function to execute.</param>
    /// <param name="task">The scheduled task to run.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method is called internally by the scheduler to manage the execution of scheduled tasks.
    /// It ensures that tasks are run at the correct times and handles any exceptions that may occur during execution.
    /// </remarks>
    private async Task SafeRun(Func<CancellationToken, Task> work, ScheduledTask task, CancellationToken ct)
    {
        try
        {
            // If cancellation was requested after the loop's check and before entering here, bail out.
            if (ct.IsCancellationRequested)
            {
                return;
            }
            var runStartedAt = DateTimeOffset.UtcNow; // capture start time
            await work(ct);

            // compute next run (only if still scheduled). We compute fully *before* publishing
            // any timestamp changes so snapshots never see LastRunAt > NextRunAt.
            DateTimeOffset nextRun;
            if (task.Interval != null)
            {
                task.RunIteration++; // increment completed count
                var interval = task.Interval.Value;
                var next = task.AnchorAt + ((task.RunIteration + 1) * interval);
                var now = DateTimeOffset.UtcNow;
                while (next - now <= TimeSpan.Zero)
                {
                    task.RunIteration++;
                    next = task.AnchorAt + ((task.RunIteration + 1) * interval);
                    if (task.RunIteration > 10_000) { break; }
                }
                nextRun = next;
            }
            else
            {
                nextRun = task.Cron is not null ? task.Cron.GetNextOccurrence(runStartedAt, _tz) ?? DateTimeOffset.MaxValue : DateTimeOffset.MaxValue;
            }

            // Publish timestamps together to avoid inconsistent snapshot (race seen in CI where
            // LastRunAt advanced but NextRunAt still pointed to prior slot).
            task.LastRunAt = runStartedAt;
            task.NextRunAt = nextRun;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* ignore */ }
        catch (Exception ex)
        {
            _log.Error(ex, "[Scheduler] Job '{Name}' failed", task.Name);
        }
    }

    /// <summary>
    /// Disposes the scheduler and cancels all running tasks.
    /// </summary>
    /// <remarks>
    /// This method is called to clean up resources used by the scheduler service.
    /// It cancels all scheduled tasks and disposes of the runspace pool manager.
    /// </remarks>
    public void Dispose()
    {
        CancelAll();
        _pool.Dispose();
        _log.Information("SchedulerService disposed");
    }
}
