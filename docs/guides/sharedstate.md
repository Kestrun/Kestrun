---
layout: default
title: Shared State & Runspaces
parent: Guides
nav_order: 70
---

# Shared State & PowerShell Runspaces

Kestrun executes PowerShell route code inside *isolated runspaces* drawn from a
managed pool. Each incoming request gets a fresh runspace (with pre-loaded
variables like `$Context`) which is disposed (or returned) after the route
finishes. This isolation prevents accidental cross-request variable leakage
while still letting you share *explicit* state safely via the Shared State
subsystem.

## 1. Runspace Isolation Model

- **Per-request runspace**: A route script runs in its own PowerShell `Runspace` drawn from a pool.
- **Clean scope**: Local variables defined in one request are not visible in another.
- **Pre-injected context**: `$Context` (HTTP request/response wrapper) and any globally registered objects are injected on creation.
- **Return semantics**: Output written to the pipeline is captured and formatted into HTTP responses by helper cmdlets (e.g. `Write-KrJsonResponse`).

Without a deliberate sharing mechanism, concurrency can lead to race
conditions or stale data if you try to reuse global variables. Kestrun solves
this via *Shared State*.

## 2. Shared State Fundamentals

Shared State is a host-level key/value store exposed to every runspace at
request start. Keys are registered with `Set-KrSharedState` and materialize as
variables you can read or mutate, typically Hashtable-based.

```powershell
# Register a shared counter table
Set-KrSharedState -Name 'Visits' -Value @{ Count = 0 }

# Later in any route
$Visits.Count  # => current accumulated count
```

### Why reference types?

Hashtable (and other reference types) allow atomic or synchronized mutations.
Pure value types (e.g. a raw `[int]`) are copied into the runspace; writing back
would require an explicit reset. Wrapping primitives inside a table/object keeps
a shared reference.

If you truly need direct value types, wrap them (e.g. `@{ Time = Get-Date }`) or use any supported option that allows value types.

## 2.1 Implicit Shared Variables & Functions (Pre-Configuration Injection)

`Set-KrSharedState` is not the only way to surface data across runspaces. Any
variable or function you define *before* calling `Enable-KrConfiguration` is
captured and injected into each runspace created for requests.

Example (see Tutorial 4.1):

```powershell
$Visits = [System.Collections.Concurrent.ConcurrentDictionary[string,int]]::new()

function Get-VisitsCount { $Visits["Count"] }

Enable-KrConfiguration   # After this point new runspaces receive $Visits and Get-VisitsCount
```

Characteristics:

- Automatically visible to route scripts (no explicit registration needed).
- Discoverable via `Get-KrSharedState -Name 'Visits'` (Kestrun surfaces them in the unified view).
- Functions defined early become globally available helpers (avoid re-defining per request).

Recommended uses:

- Static singleton utilities (e.g. caches, lookup dictionaries, configuration snapshots).
- Helper functions that wrap logging, serialization, or composite operations.

Avoid for:

- Dynamically created per-feature objects (use `Set-KrSharedState` for clarity and overwrite semantics).
- Objects whose lifecycle you must explicitly manage (creation/removal at runtime).

Deletion / Mutation:

- To remove an implicitly injected variable: set it to `$null` and optionally
    call `Remove-Variable` before re-enabling configuration (or expose a
    management route that clears it).
- Functions can be replaced by redefining them (new definition appears in subsequent runspaces). Existing in-flight requests keep their original copy.

## 2.2 Explicit vs Implicit Comparison

| Aspect | Implicit (pre-Enable-KrConfiguration) | Explicit (`Set-KrSharedState`) |
|--------|---------------------------------------|--------------------------------|
| Creation timing | Before `Enable-KrConfiguration` | Any time (startup or runtime) |
| Registration verb | None (just assign/define) | `Set-KrSharedState` cmdlet |
| Overwrite semantics | Reassignment mutates existing reference | Re-invocation replaces value (loggable) |
| Discovery | Appears in `Get-KrSharedState` | Appears in `Get-KrSharedState` |
| Removal | Manual nulling / variable removal | `Remove-KrSharedState -Name` |
| Ideal use cases | Static caches, helper functions | Counters, mutable structured state, dynamic features |
| Lifecycle control | Implicit / ad-hoc | Explicit API (clear intent) |

Guidance: prefer implicit for stable, rarely changed utilities and helper
functions; prefer explicit for mutable data whose lifecycle, atomicity, or
observability matters (e.g. metrics, shared queues, dynamic feature toggles).

## 2.3 Host-Scoped vs Global Shared State Lifecycle

Kestrun exposes **two scopes** for shared state:

| Scope | Lifetime | Visibility | Typical Use | Cleanup |
|-------|----------|------------|-------------|---------|
| Host (default) | Until host disposal (`KestrunHost` stopped/disposed) | All runspaces of that host only | Per-application counters, config, caches | Automatic when host is destroyed |
| Global (`-Global`) | Until PowerShell session ends (process exit) | All runspaces of all hosts in the same session | Cross-host metrics, coordination, process-wide feature flags | Manual via `Remove-KrSharedState -Global` or session end |

### Host-Scoped

- Stored on the `KestrunHost.SharedState` instance.
- Disposed when the host is shut down; references become invalid.
- Ideal for state that should not leak across multiple server instances (e.g. staging vs admin API).
- Restarting a host recreates a fresh, empty store (unless you re-seed values).

### Global

- Stored in the static `GlobalStore` (`Kestrun.SharedState.GlobalStore`).
- Survives creation and destruction of any number of hosts within the same PowerShell session.
- Accessible from any runspace: retrieval does not require a server parameter (`Get-KrSharedState -Global`).
- Use for process-wide coordination (aggregate request counts, shared connection pools, multi-host locks).
- Destroyed only when the PowerShell session terminates (or explicitly cleared with `Clear()` / removal calls).

### Choosing a Scope

| Question | Pick Host | Pick Global |
|----------|-----------|-------------|
| Need isolation per server? | ✔ | ✘ |
| Want values to persist across host restarts? | ✘ | ✔ |
| Managing per-environment differences (dev vs prod host)? | ✔ | ✘ |
| Aggregating metrics for all hosts in one session? | ✘ | ✔ |
| Minimizing lifetime to avoid stale data? | ✔ | ✘ |

### Access Patterns

Host scoped:

```powershell
Set-KrSharedState -Name 'LocalCache' -Value @{ hits = 0 }
$cache = Get-KrSharedState -Name 'LocalCache'
```

Global scoped:

```powershell
Set-KrSharedState -Global -Name 'ProcessMetrics' -Value @{ TotalRequests = 0 }
$metrics = Get-KrSharedState -Global -Name 'ProcessMetrics'
```

### Cleanup & Memory Considerations

- Host destruction frees host-scoped entries automatically (GC collects them if no other references).
- Global entries remain reachable; remove large or temporary data explicitly to avoid memory growth:

```powershell
Remove-KrSharedState -Global -Name 'ProcessMetrics'
```

### Concurrency Notes

Both scopes use the same underlying synchronization primitives. Atomic helpers
(`Update-KrSynchronizedCounter`) work identically. Scope selection does **not**
change thread-safety; it only changes lifetime and visibility.

## 3. Concurrency & Atomic Operations

A naive increment like:

```powershell
$Visits.Count = [int]$Visits.Count + 1
```

is not atomic: two runspaces can read the same `Count` and both write back `N+1`, losing one increment.

Use the atomic helper:

```powershell
Update-KrSynchronizedCounter -Table $Visits -Key 'Count' -By 1
```

This delegates to underlying .NET atomic primitives (e.g., `Interlocked`) or synchronized sections ensuring each request contributes exactly one increment.

### Pattern comparison

| Pattern | Safe Under Load | Notes |
|---------|-----------------|-------|
| `$Visits.Count++` | No | Lost updates possible |
| `Update-KrSynchronizedCounter` | Yes | Scales for high RPS counters |

## 4. Lifecycle & Visibility

- **Creation**: `Set-KrSharedState` early (startup script or first route); subsequent calls to the same name overwrite by design.
- **Access**: Appears as variable (e.g. `$Visits`) in every runspace during its lifetime.
- **Removal**: `Remove-KrSharedState -Name 'Visits'` deletes it; new runspaces will not see the variable.
- **Enumeration**: `Get-KrSharedState -Name '*'` can list multiple entries.

## 5. Request Workflow Example

```powershell
New-KrServer -Name demo |
  Add-KrEndpoint -Port 5000 |
  Set-KrSharedState -Name 'Visits' -Value @{ Count = 0 } |
  Enable-KrConfiguration | Out-Null

Add-KrMapRoute -Pattern '/visit' -HttpVerb POST -Code @'
    Update-KrSynchronizedCounter -Table $Visits -Key Count -By 1
    Write-KrJsonResponse @{ visits = $Visits.Count }
'@

Add-KrMapRoute -Pattern '/state/{name}' -HttpVerb GET -Code @'
    $name = Get-KrRequestRouteParam -Name 'name'
    $value = Get-KrSharedState -Name $name
    if ($null -eq $value) { Write-KrJsonResponse @{ error = 'not found' } -StatusCode 404; return }
    Write-KrJsonResponse @{ name = $name; value = $value }
'@

Start-KrServer | Out-Null
```

## 6. C# Parity

C# routes can manipulate shared state via equivalent helpers or direct host APIs (example conceptually; actual API surface may vary):

```csharp
host.SharedState.Set("Visits", new Hashtable { { "Count", 0 } });

host.AddMapRoute("/visit", HttpVerb.Post, ctx =>
{
    var visits = (Hashtable)host.SharedState.Get("Visits");
    host.SharedState.AtomicIncrement(visits, "Count", 1); // hypothetical helper
    ctx.Response.WriteJsonResponse(new { visits = (int)visits["Count"] });
});
```

## 7. Best Practices

- **Use Hashtables / custom objects** for mutable shared data.
- **Keep payloads small**: Large objects increase GC pressure and lock contention.
- **Prefer atomic helpers** for counters or metrics.
- **Wrap value types**: Avoid direct value-type sharing unless supported.
- **Avoid per-request re-registration**: Set once; reuse.
- **Validate existence**: Gracefully handle missing entries (404 or default).
- **Consider sessions for per-user state**: Shared State is *global* across all requests.

## 8. Common Pitfalls

| Symptom | Cause | Fix |
|---------|-------|-----|
| Lost counter increments | Non-atomic `++` | Use `Update-KrSynchronizedCounter` |
| Stale date/time | Cached value type copy | Wrap in reference (`@{ Time = Get-Date }`) |
| Variable missing in route | Not registered before request | Register at startup |
| Race during complex updates | Multiple field changes without guard | Perform grouped mutations inside synchronized helper or design for immutability |

## 9. Performance Considerations

- Atomic increments are O(1); contention shows only at extreme RPS.
- Minimize high-frequency writes to multi-field tables; favor single counters plus periodic aggregation.
- Remove unused state early to free references.

## 10. Testing Shared State

Leverage Pester tests that fire parallel requests (see Tutorial 4.2) to confirm final counts and absence of race conditions.

Example assertion snippet:

```powershell
$before = $Visits.Count
Invoke-ConcurrentRequests -Uri 'http://localhost:5000/visit' -Count 25
$Visits.Count -eq ($before + 25) | Should -BeTrue
```

## 11. Troubleshooting Cheatsheet

| Issue | Explanation | Action |
|-------|-------------|--------|
| Final count lower than requests | Lost updates | Switch to atomic helper |
| Variable null mid-request | Removed concurrently | Re-create or guard deletes |
| Serialization oddities | Complex object not serializable | Return simplified DTO |
| High CPU in contention | Too many simultaneous writers | Batch or shard counters |

---

Return to the [Guides index](./index).

**Related Tutorials:**

| Tutorial | Purpose |
|----------|---------|
| [4.1 Shared Variables](/pwsh/tutorial/4.variable/1.Shared-Variables) | Implicit variable/function injection pre-configuration |
| [4.2 Shared State](/pwsh/tutorial/4.variable/2.Shared-State) | Explicit cmdlet-based shared state & atomic counters |
