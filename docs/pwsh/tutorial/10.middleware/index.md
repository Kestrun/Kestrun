---
title: Middleware
parent: Tutorials
nav_order: 10
---

# Introduction to Middleware

Middleware in Kestrun are pluggable components that run in the HTTP pipeline to inspect, transform, short‑circuit,
or enrich requests and responses before / after your route handlers execute.

Core goals:

- Composition: Add only what you need (logging, antiforgery, auth, static files, caching, etc.)
- Clarity: PowerShell cmdlets wrap consistent underlying ASP.NET Core services
- Override: Most features can be tuned per route (e.g., disable antiforgery on a JSON echo endpoint)

## Current Status

| Area | Middleware Available | Notes |
|------|----------------------|-------|
| Security | Antiforgery (CSRF) | Production ready |
| Static Content | File Server | Directory browse + headers |
| Responses | Caching helpers | Per‑route directives (see Caching chapter) |
| Logging | Sinks / enrichment | See Logging section |
| AuthN/Z | Schemes & policies | See Authentication section |
| Upcoming | Rate Limiting | Planned |
| Upcoming | Compression | Planned |
| Upcoming | CORS | Planned |

## Quick Start: Antiforgery

Add antiforgery middleware + token endpoint:

```powershell
Add-KrAntiforgeryMiddleware -CookieName '.Kestrun.AntiXSRF' -HeaderName 'X-CSRF-TOKEN'
Add-KrAntiforgeryTokenRoute -Path '/csrf-token' | Out-Null
```

Protects unsafe verbs (POST/PUT/PATCH/DELETE). Fetch a token, then send it as a header with the antiforgery cookie.

See the full tutorial: [Antiforgery Protection](./1.Antiforgery)

## Pipeline Order Guidelines

Recommended relative ordering when composing:

1. Logging / correlation
2. Static files / file server
3. Security (Antiforgery, Authentication, Authorization)
4. Caching / response manipulation
5. Routing + handlers (PowerShell / C#/VB)
6. Post‑processing (metrics, custom tail middleware)

## Roadmap & Tracking

Planned middleware will appear here as they are implemented. Contributions welcome (see contributing guide).

| Feature | Status | Tracking |
|---------|--------|----------|
| Antiforgery | ✅ Implemented | Tutorial + Topic Deep Dive (coming) |
| Rate Limiting | ⏳ Planned | TBD |
| Compression | ⏳ Planned | TBD |
| CORS | ⏳ Planned | TBD |
| Request Metrics | Ideation | TBD |

## Next Steps

- Read the [Antiforgery Protection](./1.Antiforgery) chapter
- Explore caching in [Responses: Caching & Revalidation](/pwsh/tutorial/9.Responses/8.Caching)
- Review logging enrichment for cross‑request correlation

Return to the [Tutorial index](/pwsh/tutorial/index).
