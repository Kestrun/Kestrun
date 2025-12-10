---
layout: default
title: Client Sessions
parent: Guides
nav_order: 70
---

# Client Sessions

Sessions provide per-client, cookie-based state across multiple HTTP requests.
They’re ideal for small, transient data like counters, selected items, feature flags, or the current user identity after login.
In ASP.NET Core and in Kestrun, session values are stored server-side in a cache and keyed by a session ID carried in a cookie.

## Overview

- Server-side store: Backed by an `IDistributedCache` using a cookie that holds only the session ID.
- Keep it small: Suitable for a few KB of data. For larger payloads, prefer a database or distributed cache directly.
- Secure cookies: Configure `Secure`, `HttpOnly`, and an appropriate `SameSite` value; pick a unique cookie name per app.
- Lifecycle: Sessions are created on demand, loaded on first access, and persisted at the end of the request if modified.

## Enabling Sessions

```powershell
# Cookie builder (recommended)
$cookie = New-KrCookieBuilder -Name 'Kr.Session' -HttpOnly -SameSite Lax -SecurePolicy Always

# Register services + middleware, using in-memory cache by default
Add-KrSession -Cookie $cookie -IdleTimeout 20 -IOTimeout 10
```

> If you are wiring Redis/SQL yourself, add your cache provider first and use `-NoDistributedMemoryCache`.

### Provider options

Kestrun supports multiple `IDistributedCache` providers for sessions:

- In-memory (single node): Default when you call `Add-KrSession` without `-NoDistributedMemoryCache`.
- Redis (scale-out): `Add-KrDistributedRedisCache -Configuration 'host:port' -InstanceName 'App_'`.
- SQL Server (scale-out & durable): `Add-KrDistributedSqlServerCache -ConnectionString '...' -SchemaName dbo -TableName Sessions`.

In C#, register one provider before adding sessions:

```csharp
// Choose one
_ = host.AddDistributedMemoryCache(null);
// or
_ = host.AddStackExchangeRedisCache(new RedisCacheOptions { Configuration = "localhost:6379", InstanceName = "App_" });
// or
_ = host.AddDistributedSqlServerCache(new SqlServerCacheOptions { ConnectionString = "...", SchemaName = "dbo", TableName = "Sessions" });

// Then add sessions
_ = host.AddSession(new SessionOptions
{
    Cookie = new CookieBuilder { Name = "Kr.Session", HttpOnly = true, SameSite = SameSiteMode.Lax, SecurePolicy = CookieSecurePolicy.Always },
    IdleTimeout = TimeSpan.FromSeconds(20),
    IOTimeout = TimeSpan.FromSeconds(10)
});
```

## Usage Patterns

```powershell
# Counter (int)
$n = Get-KrSessionInt32 -Key 'counter'
Set-KrSessionInt32 -Key 'counter' -Value ($n + 1)

# Identity (string)
$user = Get-KrRequestQuery -Name 'user'
if ($user) { Set-KrSessionString -Key 'user' -Value $user }

# Retrieve
$who = Get-KrSessionString -Key 'user'

# Clear session
Clear-KrSession
```

## How sessions work under the hood

1. The session cookie (for Kestrun samples, `Kr.Session`) stores only a random session ID.
2. On first access to `ISession` in a request, the middleware loads a small key/value dictionary from the configured `IDistributedCache` using that ID.
3. Your code reads/writes values through `ISession` (byte[], with helpers for strings/ints in PowerShell).
4. At the end of the request, if the session was accessed and changed,
the middleware writes the updated dictionary back to the cache and ensures the cookie is set.
5. Sliding expiration: Reading/writing can extend `IdleTimeout`; on inactivity beyond `IdleTimeout`,
the session is abandoned and a new one will be created on next access.

Concurrency and locking:

- ASP.NET Core session employs an exclusive write lock per session to avoid conflicting writes.
Parallel requests for the same session can be serialized, so avoid long-running synchronous work while holding the session.
- Consider minimizing the window between reading/writing session data and returning the response to reduce contention.

Serialization:

- `ISession` stores byte arrays. Kestrun’s helpers (`Set-KrSessionString`, `Set-KrSessionInt32`, etc.) provide convenient primitives.
- For complex types, serialize to JSON or another format and store as bytes/strings.

Middleware ordering:

- The session middleware must run before any route code that accesses `ISession`.
Kestrun’s `Add-KrSession` and C# `AddSession(...)` ensure `UseSession()` is registered early.

## Kestrun implementation details

- C# extensions (`Kestrun.Hosting.KestrunHostSessionExtensions`):
  - `AddDistributedMemoryCache(MemoryDistributedCacheOptions?)`
  - `AddStackExchangeRedisCache(RedisCacheOptions)`
  - `AddDistributedSqlServerCache(SqlServerCacheOptions)`
  - `AddSession(SessionOptions?)` registers services and enqueues `UseSession()`
  - Guards: Only one distributed cache implementation and only one session configuration can be registered per host instance.

- PowerShell cmdlets:
  - `Add-KrSession` (adds in-memory cache by default unless `-NoDistributedMemoryCache`)
  - `Add-KrDistributedRedisCache` (StackExchange.Redis)
  - `Add-KrDistributedSqlServerCache` (Microsoft.Extensions.Caching.SqlServer)

- In route handlers:
  - C#: access `Context.Session` (maps to `HttpContext.Session`). DI is available via `Context.HttpContext.RequestServices` if you need `IDistributedCache` directly.
  - PowerShell: use `Get/Set-KrSession*` helpers for a consistent experience.

## Cookie and Transport

- Set `SecurePolicy Always` to restrict cookies to HTTPS. For local HTTP testing, change to `None`.
- `SameSite Lax` is a safe default for most apps; set `Strict` for tighter CSRF posture or `None` for cross-site flows with HTTPS.
- Cookies contain only the session ID; values live server-side in your cache provider.
- Consider a unique cookie name and partition by subdomain when hosting multiple apps.

## Timeouts

- `IdleTimeout`: Inactivity window after which the session is abandoned (e.g., 20s in samples).
- `IOTimeout`: Maximum time allowed for reading/writing a session store.

Provider trade-offs:

- In-memory: Simple; not shared across instances; resets on process restart.
- Redis: Shared, scalable; excellent for multi-instance deployments.
- SQL Server: Shared and durable; useful when Redis isn’t available or when DB management is preferred.

## Testing Tips

- Use a single client with a cookie jar to keep the session: `curl -c jar -b jar https://...`.
- Start a fresh session by clearing the jar or using a new WebRequestSession in PowerShell.

Diagnostics:

- Increase logging to Debug to see service registration and route execution details.
- Verify provider connectivity (Redis: ping; SQL: connection open) if changes aren’t persisting.
- In C# integration tests, resolve `IDistributedCache` from DI: `Context.HttpContext.RequestServices`.

## Anti-patterns

- Storing large objects or many keys in session. Prefer a database or cache directly; keep session small and fast.
- Long, blocking operations between session reads/writes.
    This increases lock hold time and serializes concurrent requests for the same session.
- Using session for cross-user or global data. Session is per-client; use shared state or a cache/DB for global data.
- Putting secrets or credentials in session. Prefer secure stores/claims; session is meant for transient, non-sensitive values.
- Relying on session to enforce authentication. Use proper auth middleware; session can carry minimal identity context only.

## Minimal C# route example (Kestrun)

```csharp
// Register a diagnostic route that touches session and validates cache DI
host.AddMapRoute("/diag/session", HttpVerb.Get, @"
    // TryGetValue via ISession
    var key = \"color\";
    if (!Context.Session.TryGetValue(key, out var bytes))
    {
        Context.Session.SetString(key, \"blue\");
    }
    var current = Context.Session.GetString(key) ?? \"(null)\";

    // Inspect IDistributedCache via DI (for troubleshooting)
    var sp = Context.HttpContext.RequestServices;
    var cache = (Microsoft.Extensions.Caching.Distributed.IDistributedCache?)
        sp.GetService(typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache));
    var cacheType = cache?.GetType().FullName ?? \"(none)\";

    // Quick ping write to ensure cache is writable
    Microsoft.Extensions.Caching.Distributed.DistributedCacheExtensions.SetString(cache!, \"__ping\", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

    Context.Response.WriteJsonResponse(new { session = current, cacheType });
", ScriptLanguage.CSharp);
```

## Request flow (diagram)

``` text
Client ──(Cookie: SessionId)──> Kestrun (UseSession)
   │                               │
   │                               ├─ Load session by ID from IDistributedCache
   │                               │      (in-memory | Redis | SQL Server)
   │                               │
   │                               ├─ Route handler runs
   │                               │    ├─ reads/writes ISession
   │                               │    └─ optional DI: IDistributedCache
   │                               │
   │                               └─ Commit: persist changes to cache, issue/update cookie
   │
<──└────────── Response (Set-Cookie if needed) ───────────────────────────────
```

## External references

- ASP.NET Core session and state management: <https://learn.microsoft.com/aspnet/core/fundamentals/app-state>
- IDistributedCache overview: <https://learn.microsoft.com/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache>
- Distributed Redis cache: <https://learn.microsoft.com/aspnet/core/performance/caching/distributed#distributed-redis-cache>
- Distributed SQL Server cache: <https://learn.microsoft.com/aspnet/core/performance/caching/distributed#distributed-sql-server-cache>

## Examples

- Base tutorial: [Sessions middleware and routes](/pwsh/tutorial/19.Session/1.Sessions)
- Redis provider: [Sessions with Redis](/pwsh/tutorial/19.Session/2.Sessions-Redis)
- SQL Server provider: [Sessions with SQL Server](/pwsh/tutorial/19.Session/3.Sessions-Sql)

## References

- [Add-KrSession](/pwsh/cmdlets/Add-KrSession)
- [New-KrCookieBuilder](/pwsh/cmdlets/New-KrCookieBuilder)
- [Get-KrSessionInt32](/pwsh/cmdlets/Get-KrSessionInt32)
- [Set-KrSessionInt32](/pwsh/cmdlets/Set-KrSessionInt32)
- [Get-KrSessionString](/pwsh/cmdlets/Get-KrSessionString)
- [Set-KrSessionString](/pwsh/cmdlets/Set-KrSessionString)
- [Clear-KrSession](/pwsh/cmdlets/Clear-KrSession)
- [Add-KrDistributedRedisCache](/pwsh/cmdlets/Add-KrDistributedRedisCache)
- [Add-KrDistributedSqlServerCache](/pwsh/cmdlets/Add-KrDistributedSqlServerCache)

---

Return to the [Guides index](./index).
