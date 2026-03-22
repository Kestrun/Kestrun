---
title: Middleware
parent: Tutorials
nav_order: 15
---

# Introduction to Middleware

Middleware in Kestrun is a set of pluggable pipeline components that can inspect, transform, short-circuit,
or enrich requests and responses before or after your route handlers execute.

Core goals:

- Composition: Add only what you need (logging, antiforgery, auth, static files, caching, and more).
- Clarity: PowerShell cmdlets wrap consistent ASP.NET Core middleware concepts.
- Control: Most features can be tuned globally and, where supported, per route.

## Current Status

| Area | Middleware Available | Notes |
|------|----------------------|-------|
| Security | Antiforgery (CSRF), HSTS, HTTPS Redirection | Production ready |
| Static Content | File Server | Directory browsing + headers |
| Responses | Caching helpers | Per-route directives; see the Responses caching chapter |
| Logging | Sinks / enrichment | See the Logging section |
| AuthN/Z | Schemes & policies | See the Authentication section |
| Sessions | Session state | See the Sessions chapter |
| Event streaming | SSE + Broadcast SSE | See [SSE](./9.Sse) and [SSE Broadcast](./10.SseBroadcast) |
| Security | Host Filtering | [Tutorial](./6.Host-Filtering) |
| Security | CORS | [Tutorial](./8.Cors) |
| Compression | Request Decompression (`Content-Encoding`) | [Tutorial](./11.Request-Decompression) |
| Upcoming | Rate Limiting | Planned |
| Upcoming | Compression | Planned |

## Quick Start: Antiforgery

Add antiforgery middleware and expose a token endpoint:

```powershell
Add-KrAntiforgeryMiddleware -CookieName '.Kestrun.AntiXSRF' -HeaderName 'X-CSRF-TOKEN'
Add-KrAntiforgeryTokenRoute -Path '/csrf-token' | Out-Null
```

This protects unsafe verbs (`POST`, `PUT`, `PATCH`, `DELETE`). Fetch a token first, then send it back in the configured header together with the antiforgery cookie.

See the full tutorial: [Antiforgery Protection](./1.Antiforgery)

## Pipeline Order Guidelines

Recommended relative ordering when composing middleware:

1. Logging / correlation
2. Forwarded headers / proxy normalization
3. Static files / file server
4. Security (antiforgery, authentication, authorization, CORS)
5. Caching / response manipulation
6. Routing + handlers (PowerShell / C# / VB)
7. Post-processing (metrics, custom tail middleware)

## Roadmap & Tracking

Planned middleware will appear here as they are implemented. Contributions are welcome; see the contributing docs.

| Feature | Status | Tracking |
|---------|--------|----------|
| Antiforgery | ✅ Implemented | Tutorial + deeper guide planned |
| HSTS & HTTPS Redirection | ✅ Implemented | [Tutorial](./4.Https-Hsts) + [Guide](/guides/hsts) |
| Rate Limiting | ⏳ Planned | TBD |
| Compression | ⏳ Planned | [Tutorial](./2.Compression) |
| CORS | ✅ Implemented | [Tutorial](./8.Cors) + [Guide](/guides/cors) |
| Request Metrics | Ideation | TBD |

## Next Steps

- Read the [Antiforgery Protection](./1.Antiforgery) chapter.
- Explore [Host Filtering](./6.Host-Filtering).
- Learn CORS policies in [CORS](./8.Cors).
- Learn about [HTTPS and HSTS Security](./4.Https-Hsts).
- Explore [Sessions](/pwsh/tutorial/19.Sessions/1.Sessions-Basic).
- Review caching in [Responses: Caching & Revalidation](/pwsh/tutorial/9.Responses/8.Caching).
- Review logging enrichment for cross-request correlation.

Return to the [Tutorial index](/pwsh/tutorial/index).
