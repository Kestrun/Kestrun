---
layout: default
title: CORS
nav_order: 22
---

# CORS

Cross-Origin Resource Sharing (CORS) controls which web origins (scheme, host, port) are permitted to access your HTTP APIs from browsers.
This guide explains CORS concepts, how it works in Kestrun, and recommended patterns.

## Overview

- **Purpose**: Limit which front-end origins can call your API via browser `fetch()` or XHR.
- **Origin**: A triplet of `scheme://host:port` (e.g., `http://localhost:5000`). Matching is exact.
- **Scope**: Enforced by browsers. Servers emit CORS headers; browsers decide whether to allow responses.
- **Preflight**: Browsers send an `OPTIONS` request to check permissions for certain methods/headers before the actual request.

## How CORS Works

When a browser makes a cross-origin request:

- If it's a "simple" request (e.g., `GET` with standard headers), the browser may skip preflight.
- If it's a non-simple request (e.g., `POST` with `Content-Type: application/json`),
    the browser sends an `OPTIONS` preflight including `Origin`, `Access-Control-Request-Method`, and `Access-Control-Request-Headers`.
- The server must respond with appropriate CORS headers (e.g., `Access-Control-Allow-Origin`, `Access-Control-Allow-Methods`, `Access-Control-Allow-Headers`).
    The browser then enforces access.

## Kestrun CORS Integration

Kestrun exposes CORS via both PowerShell cmdlets and C# APIs.
Internally, a `CorsPolicyDefined` flag ensures the CORS middleware is activated when at least one policy is registered.

- **Activation**: CORS middleware is wired only after a policy is registered. This avoids emitting CORS headers unintentionally.
- **Policies**: Define one default policy and/or multiple named policies.
- **Route binding**: Assign a named policy to specific routes or operations.

### PowerShell Patterns

- **Define default policy**:

```powershell
New-KrCorsPolicyBuilder |
    Set-KrCorsOrigin -Origins 'http://localhost:5000' |
    Set-KrCorsMethod -Methods GET |
    Set-KrCorsHeader -Any |
    Add-KrCorsPolicy -Default
```

- **Define named policy**:

```powershell
New-KrCorsPolicyBuilder |
    Set-KrCorsOrigin -Origins 'https://app.example.com' |
    Set-KrCorsMethod -Methods GET, POST, DELETE |
    Set-KrCorsHeader -Any |
    Add-KrCorsPolicy -Name 'TrustedApp'
```

- **Bind policy to route (OpenAPI attribute on functions)**:

```powershell
function Get-Items {
    [OpenApiPath(HttpVerb = 'Get', Pattern = '/items', CorsPolicy = 'TrustedApp')]
    param()
    Write-KrJsonResponse -StatusCode 200 -InputObject @{ items = @() }
}
```

- **Allow-all (use with caution)**:

```powershell
# Allows any origin, any method, any header
# Recommended only for local development or highly controlled scenarios
New-KrCorsPolicyBuilder |
    Set-KrCorsOrigin -Any |
    Set-KrCorsMethod -Any |
    Set-KrCorsHeader -Any |
    Add-KrCorsPolicy -Default

### Preflight max age (cache OPTIONS)

Reduce browser preflight (OPTIONS) frequency by declaring a max age.
You can specify the duration directly via `-Seconds`, or pass a `TimeSpan`.

```powershell
# 2 hours via seconds
New-KrCorsPolicyBuilder |
    Set-KrCorsOrigin -Origins 'http://localhost:3000' |
    Set-KrCorsMethod -Methods GET, POST, PUT |
    Set-KrCorsHeader -Any |
    Set-KrCorsPreflightMaxAge -Seconds 7200 |
    Add-KrCorsPolicy -Name 'WithMaxAge'

# Equivalent using TimeSpan
New-KrCorsPolicyBuilder |
    Set-KrCorsOrigin -Origins 'http://localhost:3000' |
    Set-KrCorsMethod -Methods GET, POST, PUT |
    Set-KrCorsHeader -Any |
    Set-KrCorsPreflightMaxAge -MaxAge (New-TimeSpan -Hours 2) |
    Add-KrCorsPolicy -Name 'WithMaxAge'
```

Browsers honor `Access-Control-Max-Age` but may cap or ignore large values.

### Expose custom headers (browser access)

Browsers only expose a small set of safe response headers to JavaScript.
To read application-specific headers from `fetch()`/XHR responses (e.g., pagination counts),
add them to the expose list using `Set-KrCorsExposedHeader`.

```powershell
New-KrCorsPolicyBuilder |
    Set-KrCorsOrigin -Origins 'http://localhost:3000' |
    Set-KrCorsMethod -Any |
    Set-KrCorsHeader -Any |
    Set-KrCorsExposedHeader -Headers 'X-Total-Count','X-Page-Number' |
    Add-KrCorsPolicy -Name 'WithExposedHeaders'
```

This emits `Access-Control-Expose-Headers: X-Total-Count, X-Page-Number` so browser JavaScript
can read those headers from responses.

```

### C# Patterns

- **Default allow-all (use with caution)**:

```csharp
var host = new KestrunHost(new LoggerConfiguration().CreateLogger());
host.AddCorsDefaultPolicyAllowAll();
```

- **Builder-based policy**:

```csharp
var host = new KestrunHost(new LoggerConfiguration().CreateLogger());
var builder = new Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder()
    .WithOrigins("https://app.example.com")
    .WithMethods("GET", "POST")
    .AllowAnyHeader();

host.AddCorsPolicy("TrustedApp", builder);
```

- **Action-based policy**:

```csharp
host.AddCorsPolicy("TrustedAdmin", b =>
{
    b.WithOrigins("https://admin.example.com")
     .WithMethods("GET", "POST", "DELETE")
     .AllowAnyHeader()
    .WithExposedHeaders("X-Total-Count", "X-Page-Number")
    .SetPreflightMaxAge(TimeSpan.FromHours(2))
     .AllowCredentials();
});
```

## Configuration Guidance

- **Exact origin matching**: `http://localhost:5000` is not equal to `http://127.0.0.1:5000`. Use the precise origin your browser sends.
- **Prefer named policies**: Assign granular policies per route group; avoid global allow-all.
- **Preflight support**: Include required methods and headers; consider `Access-Control-Max-Age` when appropriate.
- **Credentials**: Only enable with specific origins; credentials are not allowed with wildcard `*` origins.
- **Default policy**: Acts as fallback for routes without explicit policy.

## Common Pitfalls

- **Missing middleware**: No headers emitted if no policy is registered.
- **Wrong origin**: Mismatch between UI host and API CORS origins (e.g., `localhost` vs `127.0.0.1`).
- **Headers not allowed**: Custom headers (e.g., `Authorization`) require explicit allow.
- **Method not permitted**: Ensure the policy includes `POST`, `PUT`, `DELETE` when needed.

## Security Notes

- Avoid broad `AllowAll` in production.
- Scope policies to exact origins and necessary methods.
- Revisit policies when deploying behind proxies/CDNs where the browser-facing origin changes.

## Examples

See the tutorial for a multi-policy setup with a browser test UI:

- [CORS Middleware Tutorial](../pwsh/tutorial/10.middleware/8.Cors.md)
- Full script include: [`docs/_includes/examples/pwsh/10.8-Cors-Multipolicy.ps1`](../_includes/examples/pwsh/10.8-Cors-Multipolicy.ps1)

## References

- [New-KrCorsPolicyBuilder](/pwsh/cmdlets/New-KrCorsPolicyBuilder)
- [Set-KrCorsOrigin](/pwsh/cmdlets/Set-KrCorsOrigin)
- [Set-KrCorsMethod](/pwsh/cmdlets/Set-KrCorsMethod)
- [Set-KrCorsHeader](/pwsh/cmdlets/Set-KrCorsHeader)
- [Set-KrCorsExposedHeader](/pwsh/cmdlets/Set-KrCorsExposedHeader)
- [Set-KrCorsPreflightMaxAge](/pwsh/cmdlets/Set-KrCorsPreflightMaxAge)
- [Add-KrCorsPolicy](/pwsh/cmdlets/Add-KrCorsPolicy)
- [Add-KrMapRoute](/pwsh/cmdlets/Add-KrMapRoute)

### External Resources

- MDN: [HTTP access control (CORS)](https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS)
- MDN: [CORS preflight requests](https://developer.mozilla.org/en-US/docs/Glossary/Preflight_request)
- ASP.NET Core: [Enable Cross-Origin Requests (CORS)](https://learn.microsoft.com/aspnet/core/security/cors)
- W3C: [Fetch Standard](https://fetch.spec.whatwg.org/#http-cors-protocol)
