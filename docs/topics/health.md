---
title: Health Monitoring
parent: Guides
nav_order: 35
---

# Health Monitoring & Probes

Kestrun ships with a built-in health endpoint capable of aggregating probes written in PowerShell, C#,
HTTP callbacks, or external processes. This guide explains the moving pieces, how to compose them, and
operational patterns for reliable diagnostics in production.

## Architecture Overview

| Component | Purpose | Notes |
|-----------|---------|-------|
| `KestrunHostHealthExtensions.AddHealthEndpoint` | Adds the HTTP endpoint (default `/health`) | Converts `HealthEndpointOptions` into an ASP.NET Core route. |
| `KestrunHost.AddProbe(...)` | Registers probes (script, delegate, custom `IProbe`) | Stored in an internal list guarded by a lock for thread safety. |
| `HealthProbeRunner` | Executes probes concurrently, enforces timeouts, aggregates results | Returns a JSON-ready `HealthProbeReport`. |
| Probes (`ScriptProbeFactory`, `HttpProbe`, `ProcessProbe`, `DiskSpaceProbe`, custom `IProbe`) | Provide actual liveness checks | Each probe returns `ProbeResult` (Status, Description, Data). |
| Query string tag filters | Allows clients to pick subsets (e.g. `/health?tag=ready`) | Accepts `tag=` or `tags=` query params. |

The endpoint serializes the report using System.Text.Json and honours `TreatDegradedAsUnhealthy`, caching
headers (`no-store`), and short-circuit options so it can live alongside your APIs.

## Registering the Endpoint

### PowerShell

```powershell
New-KrServer -Name 'health-demo'
Add-KrEndpoint -Port 5000 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime

Add-KrHealthEndpoint -Pattern '/healthz' -DefaultTags 'core' -ProbeTimeout '00:00:05' -TreatDegradedAsUnhealthy $true -OpenApiSummary 'Aggregated health'

Enable-KrConfiguration
```

### Alternate Response Formats & Negotiation

Dashboards or downstream tooling may require formats beyond JSON. You can force or negotiate among:

| Enum Value | Content Type Emitted | Notes |
|------------|----------------------|-------|
| `Json` | `application/json` | Default unless overridden. |
| `Yaml` | `application/yaml` | Useful for human diffing. |
| `Xml`  | `application/xml`  | Supports custom root element. |
| `Text` | `text/plain`       | Human-friendly summary (status + probes) – no strict schema. |
| `Auto` | Negotiated         | Examines `Accept` header (JSON > YAML > XML > Text). Fallback = JSON. |

Set `HealthEndpointOptions.ResponseContentType` (C#) or `-ResponseContentType` (PowerShell). `Auto`
performs lightweight negotiation against the first matching media type the client prefers:

1. `application/json`
2. `application/yaml` / `text/yaml`
3. `application/xml` / `text/xml`
4. `text/plain`

If none match, JSON is used. All structured formats (JSON/YAML/XML) share identical field semantics.
Plain text is for dashboards, CLIs, or curl-based quick checks.

When emitting XML you can override the root node name (default `Response`) via
`HealthEndpointOptions.XmlRootElementName` / `-XmlRootElementName`.

Formatting & size:

- Pretty (indented) by default for JSON/XML.
- Use `HealthEndpointOptions.Compress = true` or `-Compress` to compact JSON/XML (YAML & Text are already minimal).

Ordering flexibility: `Add-KrHealthEndpoint` may be invoked before or after `Enable-KrConfiguration`; deferred
mapping ensures the route is finalized during build.

Example plain text output snippet:

```text
Status: Healthy
Summary: 3 probes (3 healthy)
Probes:
  - name=Disk status=Healthy freePercent=42
  - name=Cache status=Healthy hitRatio=0.97
```

```csharp
using Kestrun.Hosting;
using Kestrun.Health;

var builder = WebApplication.CreateBuilder(args);
var host = builder.Services.AddKestrunHost();

host.AddHealthEndpoint(options =>
{
    options.Pattern = "/healthz";
    options.DefaultTags = new[] { "core" };
    options.ProbeTimeout = TimeSpan.FromSeconds(5);
    options.TreatDegradedAsUnhealthy = true;
    options.OpenApiSummary = "Aggregated health";
});

var app = builder.Build();
app.UseKestrun(host);
app.Run();
```

> **Auto-registration**: `KestrunOptions.Health.AutoRegisterEndpoint` defaults to `true`. If you prefer to
> opt-in, set it to `false` and call `AddHealthEndpoint` yourself.

## Probe Types

| Approach | PowerShell cmdlet | C# API | Typical use |
|----------|-------------------|--------|-------------|
| Inline script | `Add-KrHealthProbe -ScriptBlock { ... }` | `AddProbe(name, tags, code, ScriptLanguage.PowerShell)` | Query databases, check environment, emit structured data. |
| Inline C#/VB | `Add-KrHealthProbe -Code '...' -Language CSharp` | Same as above with language override | CPU-intensive logic, shared libraries. |
| Script file | `Add-KrHealthProbe -CodePath './Check.ps1'` | Provide code via file in C# as well | Reuse existing scripts or multi-line programs. |
| Delegate | N/A (PowerShell uses script) | `AddProbe(string name, string[]? tags, Func<CancellationToken, Task<ProbeResult>> callback)` | Use dependency-injected services to run checks. |
| Custom class | Register `IProbe` implementation | `AddProbe(IProbe probe)` | Advanced scenarios needing state retention. |
| HTTP | `Add-KrHealthHttpProbe` | `AddProbe(new HttpProbe(...))` | Call downstream service health endpoints. |
| Process | `Add-KrHealthProcessProbe` | `AddProbe(new ProcessProbe(...))` | Shell out to existing CLI diagnostics. |
| Disk (default) | (auto) | `DiskSpaceProbe` auto-registered | Monitors free space of host drive (Healthy ≥10%, Degraded 5–10%, Unhealthy <5%). |

All probes ultimately return a `ProbeResult`:

```csharp
public record ProbeResult(
    ProbeStatus Status,
    string? Description = null,
    IReadOnlyDictionary<string, object?>? Data = null);
```

`Status` is one of `Healthy`, `Degraded`, `Unhealthy`. The data dictionary is optional metadata that ends up in the JSON response.

## Endpoint Options Cheatsheet

| Option | Default | Effect |
|--------|---------|--------|
| `Pattern` | `/health` | Route exposed for the endpoint. |
| `DefaultTags` | `[]` | Applied when no query filter provided. |
| `AllowAnonymous` | `true` | If `false`, standard auth is required (set `RequireSchemes/Policies`). |
| `TreatDegradedAsUnhealthy` | `false` | When `true` degrade → HTTP 503 instead of 200. |
| `ThrowOnDuplicate` | `false` | Throw if another GET route already uses `Pattern`. |
| `RequireSchemes` / `RequirePolicies` | `[]` | Additional authentication / authorization enforcement. |
| `CorsPolicyName` / `RateLimitPolicyName` | `null` | Apply preconfigured ASP.NET Core policies. |
| `ShortCircuit` | `false` | Stop other middleware from running once health response is ready. |
| `ShortCircuitStatusCode` | `null` | Override the status used when short-circuiting. |
| `OpenApi*` | Summary/Description/OperationId/Tags/Group | Document the endpoint in generated Swagger. |
| `MaxDegreeOfParallelism` | Environment processor count | Controls concurrent probe execution. |
| `ProbeTimeout` | 15 seconds | Per probe cancellation window. |
| `AutoRegisterEndpoint` | `true` | Automatically register on host creation. |
| `DefaultScriptLanguage` | PowerShell | Used by `AddProbe` overload when `ScriptLanguage` not specified. |
| `ResponseContentType` | JSON | Force JSON, YAML, XML, Text, or Auto negotiation for the aggregate payload. |
| `XmlRootElementName` | `Response` | Root element name used when `ResponseContentType` = XML (or negotiated to XML). |
| `Compress` | `false` | If true, compact JSON/XML output (no indentation). Default false emits human‑readable output. |

Set these via `Add-KrHealthEndpoint` parameters (PowerShell) or the configuration lambda (C#).

## Query Filters & Tags

- Query parameters accept either repeated `?tag=app&tag=db` or the `tags=` shorthand with comma-separated values.
- Tags are normalized to trim whitespace and compared case-insensitively.
- The endpoint executes the union of probes whose tags intersect the requested set.
- If the filter matches zero probes the response is `Healthy` with `checks = []` to avoid unnecessary failures.

### Practical Tag Strategy

| Tag | Example Probes | Purpose |
|-----|----------------|---------|
| `ready` | Database connection, message queue, essential downstream API | Use for Kubernetes readiness checks. |
| `live` | Self loopback, worker heartbeat | Use for liveness endpoints to detect deadlocks. |
| `remote` | External dependencies | Optional filter when you only want external status. |
| `compliance` | Anti-virus, disk encryption, certificate expiry | Scheduled monitoring outside traffic path. |

Clients can request `/healthz?tag=ready` for readiness and `/healthz?tags=remote,compliance` for deeper reports.

## Response Shape

Sample JSON payload (note: `Degraded` still returns HTTP 200 unless `TreatDegradedAsUnhealthy=true`):

```json
{
  "status": "Healthy",
  "duration": "00:00:00.0312500",
  "checks": [
    {
      "name": "Self",
      "status": "Healthy",
      "duration": "00:00:00.0008421",
      "description": "Main pipeline ready",
      "tags": ["self"],
      "data": null
    },
    {
      "name": "Ping",
      "status": "Healthy",
      "duration": "00:00:00.0123456",
      "description": "HTTP 200",
      "tags": ["remote", "self"],
      "data": {
        "latency": 12
      }
    }
  ],
  "tagsApplied": ["self"],
  "treatDegradedAsUnhealthy": true
}
```

Fields:

- `status` – aggregate (highest severity among probes).
- `duration` – total elapsed time to execute selected probes.
- `checks[]` – per-probe results, sorted by completion order.
- `tagsApplied` – actual filter used (defaults when none supplied).
- `treatDegradedAsUnhealthy` – copy of current endpoint setting (helps dashboards interpret HTTP status).

## Security & Governance

1. **Authentication/Authorization** – Use `-AllowAnonymous:$false`, `-RequireSchemes`, and `-RequirePolicies` to align with organisational requirements.
2. **CORS/Rate Limiting** – Attach policies when the endpoint is exposed externally.
3. **Anti-forgery** – Disabled automatically; health traffic is typically machine-to-machine.
4. **Logging** – Every probe registration logs `Registered health probe {ProbeName}` (Informational). Failures log exceptions with the probe context.
5. **OpenAPI** – Populate `OpenApi*` metadata so the endpoint appears in generated docs with the correct group/tag.

## Advanced Patterns

### Custom Probe Example (C#)

```csharp
public sealed class DiskProbe : IProbe
{
    public string Name => "Disk";
    public string[] Tags => new[] { "ready", "infra" };

    public Task<ProbeResult> CheckAsync(CancellationToken ct = default)
    {
        var info = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory)!);
        var freePercent = (double)info.AvailableFreeSpace / info.TotalSize;
        var status = freePercent < 0.05 ? ProbeStatus.Unhealthy : ProbeStatus.Healthy;
        return Task.FromResult(new ProbeResult(status, $"Free space {freePercent:P0}",
            new Dictionary<string, object?> { ["freePercent"] = freePercent }));
    }
}

host.AddProbe(new DiskProbe());
```

### Default Disk Space Probe

Kestrun automatically registers a `disk` probe (name: `disk`, tags: `self`) during configuration. It inspects the
drive containing the application base directory. Thresholds:

| Free % | Status |
|--------|--------|
| ≥ 10%  | Healthy |
| ≥ 5% and < 10% | Degraded |
| < 5% | Unhealthy |

Returned data keys:

```json
{
  "path": ".../app/base/",
  "driveName": "C:\\",
  "totalBytes": 512000000000,
  "freeBytes": 73400320000,
  "freePercent": 14.34,
  "criticalPercent": 5.0,
  "warnPercent": 10.0
}
```

Disable or override by registering your own probe with the same name before `Enable-KrConfiguration` / `EnableConfiguration`.

### Process Probe Guidance

- Use for legacy scripts or system utilities.
- Default timeout is 10 seconds; override using `-Timeout` or constructor parameter.
- Exit code mapping: `0 → Healthy`, `1 → Degraded`, `2 → Unhealthy`, any other code → Unhealthy with exit code message.
- JSON body fallback: if stdout parses into `{ status, description, data }` the probe uses that instead of exit codes.

### HTTP Probe Guidance

- Provide a shared `HttpClient` when registering multiple HTTP probes to avoid socket exhaustion.
- Remote endpoint should follow the same contract (`status`, `description`, optional `data`).
  Non-contract but successful responses map to `Degraded` to avoid silent failure.
- On exception or timeout the probe returns `Unhealthy` with the exception message.

## Operations & Observability

- **Timeout tuning** – Keep `ProbeTimeout` slightly above the 99th percentile of your slowest probe. Pair with `MaxDegreeOfParallelism` to avoid CPU saturation.
- **Retry strategy** – Probes should be idempotent; include your own retry logic inside probe code if the dependency is flaky.
- **SLO dashboards** – Export JSON to Prometheus/ELK by hitting `/health` from your monitoring system and parsing `checks`.
  For threshold alerts, combine HTTP status with JSON fields.
- **Suppressing duplicates** – Registering a probe with the same name replaces the old entry (log message indicates replacement).
  Use this for hot reload scenarios.

## Tutorials & Samples

- PowerShell walkthrough: [Responses → Health Checks](/pwsh/tutorial/9.Responses/11.Health-Checks)
- Script file: [`pwsh/tutorial/examples/9.11-Health-Checks.ps1`](/pwsh/tutorial/examples/9.11-Health-Checks.ps1)
- C# sample (multi-probe) – add to your existing host using `host.AddProbe(...)` as shown above.

## Troubleshooting

| Symptom | Likely Cause | Resolution |
|---------|--------------|------------|
| Endpoint missing | `AutoRegisterEndpoint` disabled and `AddHealthEndpoint` not invoked | Call `Add-KrHealthEndpoint` / `AddHealthEndpoint`. |
| HTTP 503 even though probes healthy | `-TreatDegradedAsUnhealthy` set and a probe returning `Degraded` | Inspect probe descriptions; lower severity or disable flag. |
| Tags filter ignored | Tags contain whitespace or mismatched case | Cmdlets trim automatically; ensure you pass `-Tags 'ready','db'` or `?tags=ready,db`. |
| Script probe throws "Runspace not initialized" | PowerShell runtime not added (`Add-KrPowerShellRuntime`) | Add runtime before registering script probes. |
| HTTP probe socket exhaustion | Creating new `HttpClient` per probe | Provide shared `HttpClient` via `-HttpClient`. |
| Process probe stuck | Hanging external process | Lower timeout or improve script to honor cancellation token. |
| JSON payload missing data | Probe returned string instead of `ProbeResult` | Ensure script returns `[Kestrun.Health.ProbeResult]` or has `status/description/data` properties. |

## Next Steps

- Integrate alerts by polling `/healthz` from your monitoring platform.
- Combine with scheduling (`Register-KrSchedule`) to self-heal when a probe fails (e.g., restart a background job).
- Read the [Logging guide](./logging) to add structured telemetry around probe execution.

Return to the [Guides index](./index) or jump back to the [PowerShell tutorial](../pwsh/tutorial/index).
