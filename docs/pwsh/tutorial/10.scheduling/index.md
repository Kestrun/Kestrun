---
title: Scheduler
parent: Tutorials
nav_order: 10
---

# Kestrun Scheduler

> 🚧 **Work in Progress**
>
> This page is currently under development. Content will be expanded with guides, examples, and best practices soon.
> Thank you for your patience while we build it out.

## Overview

Kestrun’s built-in **SchedulerService** lets your server run background jobs on fixed intervals or CRON expressions, in either **C#** or **PowerShell**.
Out-of-the-box you can:

- **Register jobs** — interval or 6-field cron (seconds precision).
- **Run work in-process**:

  - *C#*: any `Func<CancellationToken,Task>` delegate.
  - *PowerShell*: inline `ScriptBlock` **or** a script file.
- **Pause / resume / cancel** individual jobs at runtime.
- **Query live status** (UTC or any time-zone) as a strongly-typed *report* or a plain `Hashtable`.
- **Drive everything from PowerShell** via a small set of cmdlets.

| C# Type / PS Cmdlet    | Purpose                                                                                                            |
| ---------------------- | ------------------------------------------------------------------------------------------------------------------ |
| **`SchedulerService`** | Core service: register, suspend, resume, cancel, snapshot, report.                                                 |
| `ScheduledTask`        | Immutable job record (name, trigger, timestamps, suspension flag).                                                 |
| **PowerShell**         | `Register-KrSchedule`, `Suspend-KrSchedule`, `Resume-KrSchedule`, `Get-KrScheduleSnapshot`, `Get-KrScheduleReport` |

---

## 1. Enabling the scheduler

```csharp
var host = new KestrunHost("MyApp");
host.EnableScheduling();          // queues the feature
host.ApplyQueuedFeatures();       // executed once before StartAsync
```

> `EnableScheduling()` creates one shared **Runspace pool** (for PS jobs)
> and attaches a `SchedulerService` instance to `host.Scheduler`.

---

## 2. Registering jobs (C#)

### 2.1  Fixed interval

```csharp
host.Scheduler.Schedule(
    name: "heartbeat",
    interval: TimeSpan.FromSeconds(10),
    job: ct =>
    {
        Log.Information("💓  {Now:O}", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    },
    runImmediately: true);
```

### 2.2  CRON (seconds-precision)

```csharp
host.Scheduler.Schedule(
    "cleanup",
    "0 0 3 * * *",                      // every day 03:00
    async ct => await CleanupAsync(ct));
```

---

## 3. Registering jobs (PowerShell)

### 3.1  Inline `ScriptBlock` (interval)

```powershell
Register-KrSchedule -Name Cache -Interval '00:15:00' {
    Write-KrInfo "⏰ Cache refresh at $(Get-Date -f o)"
}
```

### 3.2  Script file (cron)

```powershell
Register-KrSchedule -Name Nightly `
                    -Cron '0 0 3 * * *' `
                    -ScriptPath 'Scripts/Cleanup.ps1' `
                    -RunImmediately
```

---

## 4. Controlling jobs

```powershell
# Pause a job
Suspend-KrSchedule -Name Nightly

# Resume it later
Resume-KrSchedule -Name Nightly

# Delete it entirely
$host.Scheduler.Cancel("Nightly")
```

---

## 5. Reporting & snapshot

```powershell
# Strongly-typed .NET report in UTC
$report = Get-KrScheduleReport

# Same, but Pacific time and as Hashtable
Get-KrScheduleReport -TimeZoneId 'Pacific Standard Time' -AsHashtable

# Quick snapshot (wildcards allowed)
Get-KrScheduleSnapshot -Name 'ps-*' -AsHashtable |
    ConvertTo-Json -Depth 3
```

Sample JSON report (UTC):

```json
{
  "generatedAt": "2025-07-24T22:10:30Z",
  "jobs": [
    { "name":"heartbeat","lastRunAt":"2025-07-24T22:10:28Z","nextRunAt":"2025-07-24T22:10:38Z","isSuspended":false },
    { "name":"ps-inline","lastRunAt":"2025-07-24T22:10:00Z","nextRunAt":"2025-07-24T22:11:00Z","isSuspended":false },
    { "name":"nightly","lastRunAt":null,"nextRunAt":"2025-07-25T03:00:00Z","isSuspended":true }
  ]
}
```

---

## 6. API Reference (C#)

| Method                                                                                       | What it does                                             |
| -------------------------------------------------------------------------------------------- | -------------------------------------------------------- |
| `Schedule(string name, TimeSpan interval, Func<…> job, bool runImmediately=false)`           | Register interval job (C# delegate).                     |
| `Schedule(string name, string cron, Func<…> job, bool runImmediately=false)`                 | Register cron job (C# delegate).                         |
| `Schedule(string name, TimeSpan interval, ScriptBlock script, …)`                            | Register interval PowerShell job (inline).               |
| `Schedule(string name, string cron, FileInfo script, …)`                                     | Register cron job from *.ps1* file (async).              |
| `Pause(string name) / Resume(string name)`                                                   | Toggle `IsSuspended`.                                    |
| `Cancel(string name)`                                                                        | Remove job and cancel its loop.                          |
| `GetSnapshot(TimeZoneInfo? tz = null, bool asHashtable = false, params string[] nameFilter)` | Lightweight listing (optionally wildcard-filtered & HT). |
| `GetReport(TimeZoneInfo? tz = null)`                                                         | Full `ScheduleReport` (aggregated).                      |

`JobInfo`: **Name**, **LastRunAt**, **NextRunAt**, **IsSuspended**.
`ScheduleReport`: **GeneratedAt**, **Jobs\[]**.

---

## 7. PowerShell Cmdlet Reference

| Cmdlet                       | Purpose                                               | Notes                                     |
| ---------------------------- | ----------------------------------------------------- | ----------------------------------------- |
| **`Register-KrSchedule`**    | Create new schedule (interval/cron × block/file).     | Supports `-RunImmediately`.               |
| **`Suspend-KrSchedule`**     | Pause a job.                                          | Wildcards not supported (use exact name). |
| **`Resume-KrSchedule`**      | Resume a paused job.                                  | —                                         |
| **`Get-KrScheduleSnapshot`** | Quick list, optional `-Name` filter & `-AsHashtable`. | UTC by default; `-TimeZoneId` switch.     |
| **`Get-KrScheduleReport`**   | Aggregated report; `-AsHashtable` plus TZ.            | —                                         |

---

## 8. Best practices

| Tip                                                             | Why                                             |
| --------------------------------------------------------------- | ----------------------------------------------- |
| **Store timestamps internally in UTC**                          | Keeps math simple and reports consistent.       |
| **Use `WaitAsync(token)`** when invoking PowerShell             | Allows job to cancel instantly on shutdown.     |
| **Return `Task` from C# jobs**                                  | Prevents thread-pool starvation on async work.  |
| **Keep runspace pools small** (`Options.MaxSchedulerRunspaces`) | Scheduler work is usually lightweight.          |
| **Log failures inside `SafeRun`**                               | A failed job should never crash the server.     |
| **Pause, don’t delete, for temporary outages**                  | `IsSuspended` keeps next execution predictable. |
