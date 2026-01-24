---
title: Home
layout: home
nav_order: 1
description: "Kestrun Documentation."
permalink: /
---
<!-- markdownlint-disable MD033 -->
<h1 class="wordmark wordmark--gradient wordmark--glow">Kestrun</h1>
<p class="wordmark-tagline">
PowerShell brains. Kestrel speed
</p>

Kestrun jumpstarts your web automation with a fast, PowerShell-centric framework built on ASP.NET Core,
blending scriptable flexibility with modern .NET performance.
{: .fs-5 .fw-300 }

[Get started now](#getting-started){: .btn .btn-primary .fs-5 .mb-4 .mb-md-0 .mr-2 }
[View it on GitHub][Kestrun repo]{: .btn .fs-5 .mb-4 .mb-md-0 }

---

**Kestrun** is a PowerShell-integrated framework on ASP.NET Core (Kestrel) designed for REST API development.
It combines the performance of C# with the flexibility of PowerShell, making it easy to prototype, automate, and deliver production-ready APIs with confidence.

## Highlights

- **PowerShell-first routing** — author endpoints with `Add-KrMapRoute` or Razor+PS hybrids.
- **Auth built-ins** — JWT bearer, API keys, Basic, Windows/Negotiate, client certs.
- **Razor + PS** — serve `.cshtml` with `.ps1` backers.
- **Scheduling** — PowerShell and C# jobs with cron-like control.
- **Logging** — Serilog, syslog, REST; structured logs galore.
- **OpenAPI** — generate specs and serve interactive docs (Swagger UI / ReDoc / Scalar / RapiDoc / Elements).
- **Realtime** — SSE and SignalR support for push-style apps.
- **Localization** — Request-based culture resolution with `.psd1` or JSON string tables and per-key fallback across culture hierarchies.
- **Extended HTTP verbs** — includes WebDAV methods and HTTP `QUERY` (OpenAPI 3.2+) when you need a body-based search.

## Quick links

- 👉 **PowerShell Cmdlets**: [pwsh/cmdlets/](/pwsh/cmdlets/)
- 👉 **C# API**: [cs/api/](/cs/api/)
- 📚 **Tutorials**: [pwsh/tutorial/](/pwsh/tutorial/)
- 📘 **Guides**: [Logging](/guides/)

## Getting started

```powershell
# spin up Kestrun
Import-Module Kestrun
New-KrServer -Name 'MyKestrunServer'
Add-KrEndpoint -Port 5000
Enable-KrConfiguration

Add-KrMapRoute -Verbs Get -Path '/ps/hello' -ScriptBlock {
    Write-KrTextResponse -inputObject "Hello world" -statusCode 200
}
Add-KrMapRoute -Verbs Get -Path '/cs/hello' -Code @'
    Context.Response.WriteTextResponse("Hello world", 200);
'@ -Language CSharp

Start-KrServer

```

## External Documentation

- [Kestrel documentation]

- [Powershell documentation]

- [OpenAPI documentation]

[Kestrun repo]: https://github.com/kestrun/kestrun
[Kestrel documentation]: https://learn.microsoft.com/en-us/aspnet/core/?view=aspnetcore-9.0
[Powershell documentation]: https://learn.microsoft.com/en-us/powershell/
[OpenAPI documentation]:https://spec.openapis.org/oas/
