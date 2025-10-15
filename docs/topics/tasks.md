---
title: Tasks
parent: Guides
nav_order: 60
---

# Ad-hoc Tasks (Background Script Execution)

Kestrun Tasks let clients create, start, monitor, cancel, and remove short‑lived background units of work written in PowerShell, C#, or VB.NET.
They are ideal for on-demand processing (data export, cache warm, one-off maintenance) without standing up a full scheduler entry.

## Capabilities

- Multi-language: PowerShell pipeline scripts, C# inline code (last expression value), VB.NET inline code
- Decoupled create / start lifecycle for orchestration (pre-register tasks then start later)
- Live progress reporting via an injected `TaskProgress` object (percent + message)
- Cancellation across languages (PowerShell `Stop()`, C# cooperative `CancellationToken`, VB.NET awaited Task w/ cooperative delay)
- Result retrieval only after terminal states (Completed / Failed / Stopped)
- Hierarchical parent/child relationships (internal today) to gate removals until children finish
- Safe removal once terminal (with cascading child cleanup)

## Lifecycle

State machine (simplified):

```text
Created -> (Start) -> Running -> (Complete) -> Completed
                                  |                |
                                  | (Cancel)       | (Remove allowed)
                                  v                v
                                Stopped <---- Failed
```

Terminal states: `Completed`, `Failed`, `Stopped`.

Operations:

| Operation | Allowed States | Effect |
|-----------|----------------|--------|
| Create    | —              | Allocates task (Created) |
| Start     | Created        | Transitions to Running (idempotent: subsequent Start returns false) |
| Cancel    | Running        | Requests cancellation; terminal state becomes Stopped |
| GetState  | Any            | Returns current state |
| GetResult | Terminal only  | Returns result object (null pre-terminal) |
| Remove    | Terminal + all children terminal | Deletes task (and cascades) |

## Progress Model

Each task receives an injected `TaskProgress` instance with:

| Property | Meaning |
|----------|---------|
| `PercentComplete` | 0–100; setter clamps bounds |
| `StatusMessage`   | Short human-readable phase text |
| `LastUpdatedUtc`  | Timestamp of last mutation |
| `IsTerminal`      | Helper flag once completed / failed / cancelled |

Helpers used internally: `Complete(message)`, `Fail(message)`, `Cancel(message)`, `Reset()`.

Example (PowerShell route snippet):

```powershell
$TaskProgress.PercentComplete = 0
$TaskProgress.StatusMessage = 'Starting'
1..5 | ForEach-Object {
  Start-Sleep -Milliseconds 200
  $TaskProgress.PercentComplete = $_ * 20
  $TaskProgress.StatusMessage = "Phase $_"
}
$TaskProgress.PercentComplete = 100
$TaskProgress.StatusMessage = 'Done'
42
```

C# equivalent:

```csharp
for (var i = 0; i < 5; i++) {
    await Task.Delay(200, ct);
    TaskProgress.PercentComplete = (i+1) * 20;
    TaskProgress.StatusMessage = $"Phase {i+1}";
}
TaskProgress.Complete("Done");
return 42; // shows up via /tasks/result
```

## HTTP Endpoints (Tutorial Sample)

| Endpoint | Purpose | Notes |
|----------|---------|-------|
| `POST /tasks/create/ps?seconds=2` | Create a PowerShell task | Returns JSON with id/state (Created) |
| `POST /tasks/create/cs?ms=1500`   | Create a C# task | Milliseconds delay example |
| `POST /tasks/start?id={id}`       | Start a task | Returns true/false (idempotent) |
| `GET  /tasks/state?id={id}`       | Current state & basic progress | Progress fields live update |
| `GET  /tasks/result?id={id}`      | Result object only | Null until terminal |
| `POST /tasks/cancel?id={id}`      | Cancel a running task | Returns true if cancellation requested |
| `DELETE /tasks/remove?id={id}`    | Remove a task | Fails if not terminal or child running |
| `GET  /tasks/list`                | List summaries | No output/fault payload |
| `POST /tasks/run/ps?seconds=3`    | Convenience create+start | Returns task object |

> Note: Actual verbs may differ in the sample (some use GET). Align them with your API style in production.

## Result Semantics

- PowerShell: array of pipeline base objects (or null if no output)
- C#/VB.NET: value of last expression / `return` statement
- `GetResult` returns null until the task reaches a terminal state. Test coverage asserts this pre/post behavior.

## Removal Rules

Removal succeeds only when: (a) task is terminal; (b) all children (recursive) are terminal.
The service now uses a snapshot of children during recursive removal to avoid collection modification exceptions (covered by tests).

## Testing Highlights

Recent test additions (xUnit):

- Early `GetResult` is null then non-null after completion
- `StartAsync` (via `Start`) returns false when called twice
- Parent task removal fails while child running, then succeeds after child cancelled
- VB.NET task path returns expected result value (777)
- Progress integration ensures `PercentComplete` hits 100 and status transitions

These ensure regressions (like collection modification during removal or VB.NET compilation issues) are caught.

## When to Use Tasks vs Scheduling

| Use Tasks when… | Use Scheduling when… |
|-----------------|----------------------|
| On-demand, user-triggered work | Recurring or time-based work |
| Needs immediate feedback/progress | Periodic maintenance or batch jobs |
| One-shot maintenance scripts | Long-lived repeated tasks |

Tasks complement the scheduler: you can even have scheduled jobs that queue tasks for fan-out processing.

## See Also

- Tutorial: [Tasks Walkthrough](/pwsh/tutorial/20.Tasks/1.Tasks)
- Cmdlets: `Add-KrTasksService`, `New-KrTask`, `Start-KrTask`, `Get-KrTask`, `Stop-KrTask`, `Remove-KrTask`
- Guides: [Scheduling](/topics/scheduling), [Logging](/topics/logging)
