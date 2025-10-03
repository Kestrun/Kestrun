---
title: Scheduling
parent: Guides
nav_order: 50
---

# Kestrun Scheduling (Jobs & CRON)

Kestrun’s built-in Scheduler lets your server run background jobs on fixed intervals or CRON expressions, in either C# or PowerShell.

## Capabilities

- Register jobs — interval or 6‑field CRON (seconds precision)
- Run work in-process:
  - C#: any `Func<CancellationToken, Task>` delegate, or inline code compiled via Roslyn
  - PowerShell: inline `ScriptBlock` or a `.ps1` file
- Pause / resume / cancel jobs at runtime
- Query live status (UTC or any time-zone) as a strongly-typed report or PowerShell hashtable
- Operate entirely from PowerShell cmdlets or directly via C# APIs

## Enabling the scheduler (C#)

```csharp
var host = new KestrunHost("MyApp");
host.EnableScheduling();          // queues the feature
host.ApplyQueuedFeatures();       // executed once before StartAsync
```

Enabling creates a shared PowerShell Runspace pool for PS jobs and attaches a `SchedulerService` instance at `host.Scheduler`.

## Registering jobs

### PowerShell (inline)

```powershell
Register-KrSchedule -Name Cache -Interval '00:15:00' -ScriptBlock {
    Write-KrInfo "⏰ Cache refresh at $(Get-Date -f o)"
}
```

### PowerShell (script file via CRON)

```powershell
Register-KrSchedule -Name Nightly -Cron '0 0 3 * * *' -ScriptPath 'Scripts/Cleanup.ps1'
```

### C# (inline)

```csharp
host.Scheduler.Schedule(
    name: "heartbeat",
    interval: TimeSpan.FromSeconds(10),
    job: ct => { Log.Information("💓 {Now:O}", DateTimeOffset.UtcNow); return Task.CompletedTask; },
    runImmediately: true);
```

### C# (CRON)

```csharp
host.Scheduler.Schedule("cleanup", "0 0 3 * * *", async ct => await CleanupAsync(ct));
```

## Controlling jobs

```powershell
Suspend-KrSchedule -Name Nightly   # pause
Resume-KrSchedule -Name Nightly    # resume
```

From C#, use `Pause(name)`, `Resume(name)`, or `Cancel(name)` on `SchedulerService`.

## Reporting

```powershell
# Strongly-typed .NET report in UTC
$report = Get-KrScheduleReport

# Same, but Pacific time and as Hashtable
Get-KrScheduleReport -TimeZoneId 'Pacific Standard Time' -AsHashtable

# Quick snapshot (wildcards allowed)
Get-KrScheduleSnapshot -Name 'ps-*' -AsHashtable | ConvertTo-Json -Depth 3
```

Sample (trimmed):

```json
{
  "generatedAt": "2025-07-24T22:10:30Z",
  "jobs": [
    { "name":"heartbeat","lastRunAt":"2025-07-24T22:10:28Z","nextRunAt":"2025-07-24T22:10:38Z","isSuspended":false }
  ]
}
```

## API reference (C#)

| Method | Purpose |
|--------|---------|
| `Schedule(string name, TimeSpan interval, Func<…> job, bool runImmediately=false)` | Register interval job (C# delegate) |
| `Schedule(string name, string cron, Func<…> job, bool runImmediately=false)` | Register cron job (C# delegate) |
| `Schedule(string name, TimeSpan interval, ScriptBlock script, …)` | Interval PowerShell job (inline) |
| `Schedule(string name, string cron, FileInfo script, …)` | CRON job from .ps1 file |
| `Pause(string name)` / `Resume(string name)` | Toggle `IsSuspended` |
| `Cancel(string name)` | Remove job and stop its loop |
| `GetSnapshot(TimeZoneInfo? tz = null, bool asHashtable = false, params string[] nameFilter)` | Lightweight listing |
| `GetReport(TimeZoneInfo? tz = null)` | Full `ScheduleReport` |

Types:

- `JobInfo`: Name, LastRunAt, NextRunAt, IsSuspended
- `ScheduleReport`: GeneratedAt, Jobs[]

## PowerShell cmdlets

- `Add-KrScheduling` — enable scheduler
- `Register-KrSchedule` — create jobs (interval/cron × block/file/code)
- `Suspend-KrSchedule` / `Resume-KrSchedule` — control execution
- `Get-KrScheduleSnapshot` — quick list
- `Get-KrScheduleReport` — aggregated report

## Best practices

- Store timestamps internally in UTC — consistent math & comparisons
- Use `WaitAsync(token)` when invoking PowerShell — fast shutdowns
- Return `Task` from C# jobs — avoid thread-pool starvation
- Keep runspace pools small (`Options.MaxSchedulerRunspaces`) — jobs are usually light
- Log failures inside job bodies — a failed job should never crash the server
- Pause instead of delete for outages — preserves next run predictability

## See also

- Tutorial: [Scheduling Quickstart](/pwsh/tutorial/12.scheduling/1.Scheduling-Quickstart)
