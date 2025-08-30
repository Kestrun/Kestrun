---
title: Static Routes
parent: Tutorials
nav_order: 3
---

# Static Routes Overview

Serve pre-built assets (HTML, CSS, JS, images, docs) directly from disk without writing explicit route handlers.
Static hosting is ideal for:

- Landing / status pages
- Documentation bundles
- Front‑end single page apps (with additional routing rules)
- Images and downloadable assets

The static files service mounts a directory tree at a request path prefix. Requests under that prefix are mapped
to files relative to the configured root path.

---

## Chapters

| Order | Chapter                                      | Focus                                                  |
|-------|----------------------------------------------|--------------------------------------------------------|
| 1     | [Serving Static Files](./1.Static-Routes.md) | Basic static file service (`Add-KrStaticFilesService`) |

Upcoming chapters (planned):

- Cache & content headers
- SPA / fallback routing
- Multiple static roots & versioned assets

---

## Quick Start

From the repository root run the sample script:

```powershell
pwsh .\examples\PowerShell\Tutorial\6-Static-Routes.ps1
```

Then browse (default listener <http://127.0.0.1:5000>):

- <http://127.0.0.1:5000/assets/index.html>
- <http://127.0.0.1:5000/assets/css/site.css>

Stop the server with Ctrl+C.

---

## Key Cmdlets

- `Initialize-KrRoot` — lock working root so relative asset paths resolve reliably.
- `Add-KrStaticFilesService` — register a static file mount (`-RequestPath` + `-RootPath`).
- `Add-KrListener` / `New-KrServer` / `Enable-KrConfiguration` / `Start-KrServer` — standard server lifecycle.

---

## Next

Proceed to the first chapter: [Serving Static Files](./1.Static-Routes.md).  
After static hosting, continue to [Variable Routes](../4.variable/index.md) to handle dynamic segments.

---

Contributions welcome — open issues or PRs in the [repository](https://github.com/Kestrun/Kestrun).
