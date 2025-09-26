---
title: HTTP Caching
parent: Guides
nav_order: 40
---

# HTTP Caching in Kestrun

This guide explains the caching building blocks available in Kestrun (PowerShell & C#) and how they layer:

| Layer | Purpose | Scope | Typical Cmdlets / APIs |
|-------|---------|-------|-------------------------|
| File Server Headers | Emit baseline `Cache-Control` for every static file/directory listing | All static responses under mount | `Add-KrFileServerMiddleware` switches (`-Public`, `-MaxAge`, etc.) |
| Response Cache Middleware | Short‑lived in-memory storage + path-based reuse | Eligible dynamic (and static) responses | `Add-KrCacheMiddleware` |
| Per-Response Cache Directives | Override / refine headers for one response | Single dynamic response | `Add-KrCacheResponse` |
| Validators (ETag / Last-Modified) | Conditional 304 negotiation | Individual resource instances | `Test-KrCacheRevalidation` |

> Best practice: Start with simple immutable asset versioning (e.g. `app.v1.js`). Add conditional requests when payload size or frequency justify it.
Use per-response overrides only for endpoints with special requirements (privacy, shorter TTL, etc.).

---

## 1. File Server Cache Directives (`Add-KrFileServerMiddleware`)

When you pass cache switches to `Add-KrFileServerMiddleware` a `Cache-Control` header is created once and applied to every static response it serves.
This is coarse-grained, fast, and ideal for assets that share the same policy.

Supported switches (subset relevant to static assets):

| Switch | Header Fragment | Meaning |
|--------|-----------------|---------|
| `-Public` | `public` | Any cache (browser, shared proxy) may store the response. |
| `-Private` | `private` | Only the end-user's browser may cache. |
| `-MaxAge <sec>` | `max-age=<n>` | Freshness lifetime for browsers. |
| `-SharedMaxAge <sec>` | `s-maxage=<n>` | Freshness lifetime for shared caches (overrides `max-age` there). |
| `-NoCache` | `no-cache` | May be stored but MUST revalidate before reuse. |
| `-NoStore` | `no-store` | Do not write to any persistent storage. |
| `-MustRevalidate` | `must-revalidate` | Stale responses must be revalidated. |
| `-ProxyRevalidate` | `proxy-revalidate` | Like `must-revalidate` but for shared caches. |
| `-NoTransform` | `no-transform` | Intermediaries must not modify payload (e.g., compression). |
| `-OnlyIfCached` | `only-if-cached` | (Rare) Client will accept only a cached response. |
| `-MaxStale` / `-MaxStaleLimit` | `max-stale[=<n>]` | Client will accept a response that has exceeded its freshness. |
| `-MinFresh <sec>` | `min-fresh=<n>` | Client requires a response fresh for at least N more seconds. |

These seldom-used directives (`-OnlyIfCached`, `-MaxStale*`, `-MinFresh`) are mostly for diagnostic / edge scenarios;
prefer the core ones (`Public/Private`, `MaxAge`, `NoStore`).

Example (PowerShell):

```powershell
Add-KrFileServerMiddleware -RequestPath '/' -RootPath '.\Assets\wwwroot' -EnableDirectoryBrowsing -Public -MaxAge 300 -MustRevalidate
```

Equivalent (C# builder pattern snippet):

```csharp
// Pseudo-code: actual builder extension mirrors the PowerShell cmdlet behavior internally
app.AddFileServer(new FileServerOptions { /* file provider setup */ }, cache => {
    cache.Public = true;
    cache.MaxAge = TimeSpan.FromSeconds(300);
    cache.MustRevalidate = true;
});
```

---

## 2. Response Cache Middleware (`Add-KrCacheMiddleware`)

Adds an in-memory store for eligible responses (size ≤ `-MaximumBodySize`) with a total cap (`-SizeLimit`). It does not add validators by itself;
it just stores and replays responses with their headers while fresh.

Key parameters:

| Parameter | Purpose |
|-----------|---------|
| `-SizeLimit <bytes>` | Global cap across all cached entries. |
| `-MaximumBodySize <bytes>` | Skip caching when body exceeds this threshold. |
| `-UseCaseSensitivePaths` | Distinguish paths by case (Linux). |
| `-MaxAge` / `-SharedMaxAge` | Apply freshness lifetimes implicitly if not already present. |
| `-Public` / `-Private` | Set cacheability scope for stored responses. |

Combine with validators for optimal bandwidth reduction.

---

## 3. Per-Response Directives (`Add-KrCacheResponse`)

Use inside a route script block before writing the body. It builds a fresh `CacheControlHeaderValue` and assigns it to the current response.
This overrides broader defaults.

Parameter details and guidance:

| Parameter | Adds / Sets | Typical Usage | Notes |
|-----------|-------------|---------------|-------|
| `-Public` | `public` | Same payload for all users | Mutually exclusive with `-Private`. |
| `-Private` | `private` | User/account specific output | Avoid combining with personalization leaks. |
| `-MaxAge <sec>` | `max-age=<n>` | Short-lived UI JSON, config fragments | Pair with validators for safe reuse. |
| `-SharedMaxAge <sec>` | `s-maxage=<n>` | CDN / proxy distinct freshness | Overrides `max-age` for shared caches. |
| `-NoStore` | `no-store` | Sensitive (tokens, PII) | Overrides most other directives. |
| `-NoCache` | `no-cache` | Force revalidation | Only helpful with validators. |
| `-MustRevalidate` | `must-revalidate` | Prevent offline reuse after stale | Adds latency when often stale. |
| `-ProxyRevalidate` | `proxy-revalidate` | Shared caches enforce revalidation | Rare outside enterprise. |
| `-NoTransform` | `no-transform` | Prevent intermediary alteration | Use for media / already-compressed. |
| `-OnlyIfCached` | `only-if-cached` | Debug stale vs network fetch | Usually client-driven; seldom set server-side. |
| `-MaxStale` / `-MaxStaleLimit <sec>` | `max-stale[=<n>]` | Acceptable stale tolerance (client hint) | Typically not server-set; informational. |
| `-MinFresh <sec>` | `min-fresh=<n>` | Pre-emptively revalidate soon-to-expire content | Edge optimization. |

Example (PowerShell):

```powershell
Add-KrMapRoute -Pattern '/profile' -Verbs GET -ScriptBlock {
    Add-KrCacheResponse -Private -MaxAge 60 -MustRevalidate
    $user = @{ id = 1; name = 'demo' }
    $user | Write-KrJsonResponse
}
```

Weak ETag variant + no-store (discouraged combination; example only):

```powershell
Add-KrMapRoute -Pattern '/volatile' -Verbs GET -ScriptBlock {
    Add-KrCacheResponse -NoStore
    $payload = Get-Random
    if (-not (Test-KrCacheRevalidation -Payload $payload -Weak)) {
        Write-KrTextResponse $payload
    }
}
```

---

## 4. Validators (`Test-KrCacheRevalidation`)

Validator helper decides if the client has a current representation.

Algorithm (conceptual):

1. If `-ETag` supplied → compare vs `If-None-Match` (support list). If any strong match (or weak match when allowed) → 304.
2. Else if `-Payload` supplied → hash payload bytes → compare result vs header tokens.
3. If `-LastModified` provided → compare to `If-Modified-Since` (second precision). If not newer → 304.
4. On 304 → emits validators + returns `$true` (stop writing body). Else returns `$false` and emits validators so future requests can revalidate.

Guidelines:

| Scenario | Recommended Inputs |
|----------|--------------------|
| Immutable string/bytes | `-Payload` (automatic hash) |
| Versioned resource | `-ETag 'v2' -LastModified $deploymentTime` |
| Expensive dynamic build | Pre-compute summary hash and supply as `-ETag` |
| Time-windowed data | Use `-ETag` with rounded timestamps (e.g., minute granularity) |

Example (explicit ETag + LastModified):

```powershell
Add-KrMapRoute -Pattern '/versioned' -Verbs GET -ScriptBlock {
    if (-not (Test-KrCacheRevalidation -ETag 'v2' -LastModified (Get-Date '2024-01-01'))) {
        Write-KrTextResponse 'v2 payload'
    }
}
```

---

## 5. Putting it together (composition)

Example combining all layers:

```powershell
Initialize-KrRoot -Path $PSScriptRoot
New-KrServer -Name 'cache-demo'
Add-KrEndpoint -Port 5000 -IPAddress ([IPAddress]::Loopback)
Add-KrFileServerMiddleware -RequestPath '/' -RootPath '.\Assets\wwwroot' -Public -MaxAge 600 -MustRevalidate
Add-KrCacheMiddleware -SizeLimit 8388608 -MaximumBodySize 65536 -Public -MaxAge 60
Add-KrPowerShellRuntime
Enable-KrConfiguration

Add-KrMapRoute -Pattern '/profile' -Verbs GET -ScriptBlock {
    Add-KrCacheResponse -Private -MaxAge 30 -MustRevalidate
    $profile = @{ id = 1; name = 'demo'; ts = (Get-Date).ToUniversalTime() }
    if (-not (Test-KrCacheRevalidation -Payload ($profile.ts.ToString('O')))) {
        $profile | Write-KrJsonResponse -Compress
    }
}

Start-KrServer
```

---

## 6. C# Sketch (Validators)

```csharp
// Hypothetical usage inside a minimal API endpoint
app.MapGet("/versioned", async ctx => {
    var etag = "\"v2\""; // include quotes
    var last = new DateTimeOffset(2024,1,1,0,0,0,TimeSpan.Zero);
    if (ctx.Response.RevalidateCache("immutable", etag, false, last))
        return; // 304 already written
    ctx.Response.ContentType = "text/plain";
    await ctx.Response.WriteAsync("v2 payload");
});
```

---

## 7. Decision Checklist

| Goal | Start With | Add Later If Needed |
|------|------------|---------------------|
| Basic static assets | File server headers (`Public` + short `MaxAge`) | Versioned filenames (`app.v2.js`) |
| Dynamic JSON (same for all) | Response cache middleware | Validators (`Payload` hash) |
| User-specific JSON | `-Private` per-response | Possibly short `MaxAge` + validators |
| Sensitive data | `-NoStore` | Nothing (avoid caching) |
| CDN integration | `-Public -SharedMaxAge` | Validators for bandwidth savings |

---

## 8. Troubleshooting Summary

| Symptom | Likely Cause | Fix |
|---------|--------------|-----|
| Never see 304 | ETag changes each request | Use stable hash/version; ensure timestamps not regenerated unnecessarily |
| Validator returns `$false` unexpectedly | Missing `-Payload` / `-ETag` input | Supply correct parameter; do not pass complex objects directly |
| Private data cached publicly | `-Public` used inadvertently | Switch to `-Private` / remove directive |
| Memory growth | Large `-SizeLimit` or many large bodies | Lower limits / tune `-MaximumBodySize` |
| Stale content served | TTL too long (`MaxAge`) | Reduce value or add `MustRevalidate` |

---

## 9. Related Tutorials

See the tutorial chapters:

- [File Server Caching Headers](/pwsh/tutorial/3.static/5.File-Server-Caching)
- [Response Caching & Conditional Requests](/pwsh/tutorial/3.static/6.Response-Caching)

---

Return to the [Guides index](./index) or the [Tutorial index](/pwsh/tutorial/index).
