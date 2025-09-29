---
title: Routing & Conventions
parent: Guides
nav_order: 40
---

# Routing & Conventions

Kestrun lets you map routes from scripts (PowerShell, C#, VB, etc.), native delegates, Razor, and
static HTML templates. All of these eventually flow through a unified set of conventions:

- Authentication schemes & authorization policies
- CORS policies
- Rate limiting policies
- Anonymous / authenticated access
- Antiforgery disable switches
- Short-circuit metadata (stop further middleware)
- OpenAPI (summary, description, operation id, tags, group)
- Language metadata for discovery / diagnostics

This guide explains how to:

1. Map routes using the high-level helpers
2. Apply conventions to ad‑hoc endpoints built with `app.MapGet` / `MapPost`
3. Understand duplicate detection & replacement
4. PowerShell parity and current limitations

## 1. Mapping Routes (Script & Native)

C# minimal example:

```csharp
var builder = WebApplication.CreateBuilder(args);
var host = builder.Services.AddKestrunHost();

host.AddMapRoute("/hello", HttpVerb.Get, async ctx =>
{
    ctx.Response.StatusCode = 200;
    await ctx.Response.WriteAsync("Hello native");
}, requireSchemes: new[] { "Bearer" });

host.AddMapRoute(new MapRouteOptions
{
    Pattern = "/time",
    HttpVerbs = [ HttpVerb.Get ],
    Code = "[DateTime]::UtcNow",
    Language = ScriptLanguage.PowerShell,
    OpenAPI = new() { Summary = "UTC time" },
    AllowAnonymous = true,
    DisableAntiforgery = true
});

var app = builder.Build();
app.UseKestrun(host);
app.Run();
```

PowerShell equivalent:

```powershell
New-KrServer -Name demo
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime

Add-KrMapRoute -Pattern '/time' -HttpVerb GET -Code '[DateTime]::UtcNow' -OpenApiSummary 'UTC time' -AllowAnonymous -DisableAntiforgery

Enable-KrConfiguration
Start-KrServer | Out-Null
```

## 2. Applying Conventions to Ad‑Hoc Endpoints

If you map endpoints directly with ASP.NET Core (e.g. `app.MapGet`), you can still apply the same
Kestrun conventions using `KestrunHost.ApplyKestrunConventions`.

```csharp
var route = app.MapGet("/metrics/raw", () => Results.Ok(new { workers = 7, queue = 12 }));

host.ApplyKestrunConventions(route, opts =>
{
    opts.RequirePolicies = new[] { "MetricsViewer" };
    opts.RateLimitPolicyName = "default";
    opts.DisableAntiforgery = true;   // machine-to-machine
    opts.AllowAnonymous = false;      // enforce auth
    opts.ShortCircuit = true;
    opts.ShortCircuitStatusCode = 200; // ensure no other middleware runs
    opts.OpenAPI.Summary = "Raw internal metrics";
    opts.OpenAPI.Tags = ["metrics", "internal"];
});
```

Notes:

### Why no Pattern-Only Overload?

Once a route is built and added to the endpoint data source, its `IEndpointConventionBuilder` is not retrievable
by just the pattern + verb pair. Kestrun keeps logical registration info (`MapRouteOptions`) for duplicate detection,
but not the original mutable builder instance. Because of this, applying conventions post-hoc purely by pattern isn't
reliable (ordering and metadata emission timing become ambiguous). Instead, capture the builder when you map or re-map the route.

If you need to add conventions later:

1. Remove (or ignore) the original route.
2. Re-map with the desired options.
3. Or wrap the underlying handler in your own aggregate.

## 3. Duplicate Detection & Replacement

`AddMapRoute` records routes in an internal dictionary keyed by `(pattern, method)`. When you add another route with the
same pair:

For health probes, registering a probe with the same name replaces the prior one (log level Information).

## 4. PowerShell Parity & Limitations

`Add-KrMapRoute` mirrors the C# helpers for mapping. A PowerShell wrapper for ad‑hoc convention application is **not** provided yet
because PowerShell typically maps routes through the cmdlet itself (thus already invoking the shared convention logic). If you
hand-author endpoints in a compiled extension, apply conventions in C#.

If a future scenario requires a PowerShell `Apply-KrRouteConventions`, the design would need to surface the underlying
`IEndpointConventionBuilder` or perform a re-map. Until then, use the existing route cmdlets or create a tiny .NET helper.

## 5. OpenAPI Metadata Tips

| Field | When to Use | Effect |
|-------|-------------|--------|
| `OperationId` | Client generation / uniqueness | Sets endpoint name (`WithName`) |
| `Summary` | Brief, 1-line overview | Appears in Swagger list |
| `Description` | Detailed explanation | Expanded operation view |
| `Tags` | Group related endpoints | Supports filtering / UI grouping |
| `GroupName` | Versioning / module grouping | Creates logical doc section |

Only non-empty properties are applied. You can call `ApplyKestrunConventions` multiple times; later calls can add fields that were missing earlier.

## 6. Short-Circuiting

Set `ShortCircuit = true` and `ShortCircuitStatusCode` (required) to prevent subsequent middleware from executing.
This is useful for endpoints like `/health`, `/ready`, or fast internal metadata responses.

## 7. Antiforgery Guidance

Antiforgery is only validated for unsafe verbs (POST/PUT/PATCH/DELETE) and only when `DisableAntiforgery` is `false`.
Most machine endpoints (health, metrics, diagnostics) disable antiforgery while remaining protected by auth + rate limiting.

## 8. Troubleshooting

| Symptom | Explanation | Action |
|---------|-------------|--------|
| Conventions not applied | Builder not passed through `ApplyKestrunConventions` | Capture builder variable before chaining |
| Auth scheme error | Scheme not registered | Register scheme before mapping route |
| No OpenAPI changes | Metadata fields empty | Supply non-empty Summary/Tags/etc. |
| Short-circuit missing | Status code not set | Provide `ShortCircuitStatusCode` |
| Can't update existing route | Builder no longer accessible | Re-map route with new options |

Return to the [Guides index](./index).
