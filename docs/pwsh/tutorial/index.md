---
title: Tutorials
parent: PowerShell

nav_order: 0
---

# Tutorials

Step-by-step guides to build and ship with Kestrun. This index lists runnable sample scripts and documentation chapters in recommended learning order.

## Prerequisites

- PowerShell 7.4, 7.5, or 7.6
- .NET Runtime (SDK not required)
  - With PowerShell 7.4 or 7.5: install .NET 8 Runtime AND ASP.NET Core Runtime
  - With PowerShell 7.6: install .NET 9 Runtime AND ASP.NET Core Runtime
- Kestrun module: installed or available from this repository at `src/PowerShell/Kestrun/Kestrun.psm1`
- Supported OS: same as .NET 8/9 (Windows, Linux, macOS), including ARM/ARM64

## Quick start: run the samples

From the repository root:

```powershell
# 1) Hello World
pwsh .\examples\PowerShell\Tutorial\1-Hello-World.ps1
```

Then browse the routes (default listener: <http://127.0.0.1:5000>):
Read the note on each sample for the routes detail.

Stop the server with Ctrl+C in the terminal.

## Samples & Chapters Overview

| Order | Topic                         | Chapter                                | Sample Script               | Focus                               |
| ----- | ----------------------------- | -------------------------------------- | --------------------------- | ----------------------------------- |
| 1     | Hello World                   | [Hello World][ch-hello]                | [Script][sc-hello]          | Minimal server + 1 text route       |
| 2     | Content Types                 | [Content Types][ch-content]            | [Script][sc-content]        | JSON / XML / YAML helpers           |
| 3     | Multi-Language                | [Multi-Language Routes][ch-multilang]  | [Script][sc-multilang]      | C#/VB inline handlers               |
| 4     | Route Options                 | [Route Options][ch-routeopts]          | [Script][sc-routeopts]      | MapRouteOptions variations          |
| 5     | Static Files                  | [Static Routes][ch-static]             | [Script][sc-static]         | Host static content / wwwroot       |
| 6     | File Server                   | [File Server][ch-fileserver]           | [Script][sc-fileserver]     | Directory browsing & file responses |
| 7     | Override Routes               | [Override Routes][ch-override]         | [Script][sc-override]       | Replace static with dynamic         |
| 8     | FavIcon                       | [Add FavIcon][ch-favicon]              | [Script][sc-favicon]        | Serve favicon.ico                   |
| 9     | Shared State                  | [Shared Variables][ch-shared]          | [Script][sc-shared]         | Share state between routes          |
| 10    | Logging Basics                | [Simple Logging][ch-log-basic]         | [Script][sc-log-basic]      | Console logger, levels              |
| 11    | Logging Advanced              | [Multiple Loggers][ch-log-adv]         | [Script][sc-log-adv]        | Multiple sinks & levels             |
| 12    | Logging Enrichment            | [Enrichment & Correlation][ch-log-enr] | [Script][sc-log-enr]        | Correlation IDs                     |
| 13    | Logging Sinks                 | [Sinks][ch-log-sinks]                  | [Script][sc-log-sinks]      | Built-in sinks overview             |
| 14    | Logging Sinks Advanced        | [Sinks Advanced][ch-log-sinks-adv]     | [Script][sc-log-sinks-adv]  | Custom/advanced sinks               |
| 15    | Logging Hot Reload            | [Hot Reload][ch-log-reload]            | [Script][sc-log-reload]     | Reconfigure without restart         |
| 16    | Certificates: Self-Signed     | [Self-Signed][ch-cert-self]            | [Script][sc-cert-self]      | Dev cert generation                 |
| 17    | Certificates: CSR             | [CSR][ch-cert-csr]                     | [Script][sc-cert-csr]       | Create signing request              |
| 18    | Certificates: Import/Export   | [Import / Export][ch-cert-imp-exp]     | [Script][sc-cert-imp-exp]   | Manage PFX lifecycle                |
| 19    | Endpoints: Basic Server       | [Basic Server][ch-end-basic]           | [Script][sc-end-basic]      | Minimal hosting                     |
| 20    | Endpoints: Multiple Listeners | [Multiple Listeners][ch-end-multi]     | [Script][sc-end-multi]      | Several ports                       |
| 21    | Endpoints: HTTPS              | [HTTPS][ch-end-https]                  | [Script][sc-end-https]      | TLS listener                        |
| 22    | Endpoints: Named Pipes        | [Named Pipes][ch-end-pipes]            | [Script][sc-end-pipes]      | Windows IPC                         |
| 23    | Endpoints: Unix Sockets       | [Unix Sockets][ch-end-unix]            | [Script][sc-end-unix]       | Unix IPC / proxy                    |
| 24    | Server Configuration: Limits  | [Server Limits][ch-scfg-limits]        | [Script][sc-scfg-limits]    | Size & rate limits                  |
| 25    | Server Configuration: Options | [Server Options][ch-scfg-options]      | [Script][sc-scfg-options]   | Headers & runspaces                 |
| 26    | Lifecycle: Start/Stop         | [Start / Stop][ch-life-startstop]      | [Script][sc-life-startstop] | Non-blocking lifecycle              |
| 27    | Demo: Full Server             | [Full Server Demo][ch-demo-full]       | [Script][sc-demo-full]      | Integrated scenario                 |

Static chapters and scripts are all linked directly above for quick navigation.

[ch-hello]: ./1.introduction/index
[ch-content]: ./2.routes/1.Multiple-Content-Types
[ch-multilang]: ./2.routes/2.Multi-Language-Routes
[ch-routeopts]: ./2.routes/4.Route-Options
[ch-static]: ./3.static/1.Static-Routes
[ch-fileserver]: ./3.static/2.File-Server
[ch-override]: ./3.static/3.Static-Override-Routes
[ch-favicon]: ./3.static/4.Favicon
[ch-shared]: ./4.variable/1.Shared-Variables
[ch-log-basic]: ./5.logging/1.Simple-Logging
[ch-log-adv]: ./5.logging/2.Multiple-Loggers-Levels
[ch-log-enr]: ./5.logging/3.Enrichment-Correlation-IDs
[ch-log-sinks]: ./5.logging/4.Sinks
[ch-log-sinks-adv]: ./5.logging/5.Sinks-Advanced
[ch-log-reload]: ./5.logging/6.Hot-Reload
[ch-cert-self]: ./6.certificates/1.Self-Signed
[ch-cert-csr]: ./6.certificates/2.CSR
[ch-cert-imp-exp]: ./6.certificates/3.Import-Export-Validate
[ch-end-basic]: ./7.endpoints/1.Basic-Server
[ch-end-multi]: ./7.endpoints/2.Multiple-Listeners
[ch-end-https]: ./7.endpoints/3.Https
[ch-end-pipes]: ./7.endpoints/4.Named-Pipes
[ch-end-unix]: ./7.endpoints/5.Unix-Sockets
[ch-scfg-limits]: ./13.server-configuration/1.Server-Limits
[ch-scfg-options]: ./13.server-configuration/2.Server-Options
[ch-life-startstop]: ./14.lifecycle/1.Start-Stop
[ch-demo-full]: ./15.demos/1.Full-Demo
[sc-hello]: /_includes/examples/pwsh/1.1-Hello-World.ps1
[sc-content]: /_includes/examples/pwsh/2.1-Multiple-Content-Types.ps1
[sc-multilang]: /_includes/examples/pwsh/2.2-Multi-Language-Routes.ps1
[sc-routeopts]: /_includes/examples/pwsh/2.4-Route-Options.ps1
[sc-static]: /_includes/examples/pwsh/3.1-Static-Routes.ps1
[sc-fileserver]: /_includes/examples/pwsh/3.2-File-Server.ps1
[sc-override]: /_includes/examples/pwsh/3.3-Static-OverrideRoutes.ps1
[sc-favicon]: /_includes/examples/pwsh/3.4-Add-FavIcon.ps1
[sc-shared]: /_includes/examples/pwsh/4.1-Shared-Variables.ps1
[sc-log-basic]: /_includes/examples/pwsh/5.1-Simple-Logging.ps1
[sc-log-adv]: /_includes/examples/pwsh/5.2-Multiple-Loggers-Levels.ps1
[sc-log-enr]: /_includes/examples/pwsh/5.3-Enrichment-Correlation-IDs.ps1
[sc-log-sinks]: /_includes/examples/pwsh/5.4-Sinks.ps1
[sc-log-sinks-adv]: /_includes/examples/pwsh/5.5-Sinks-Advanced.ps1
[sc-log-reload]: /_includes/examples/pwsh/5.6-Hot-Reload.ps1
[sc-cert-self]: /_includes/examples/pwsh/6.1-Cert-SelfSigned.ps1
[sc-cert-csr]: /_includes/examples/pwsh/6.2-Cert-CSR.ps1
[sc-cert-imp-exp]: /_includes/examples/pwsh/6.3-Cert-Import-Export.ps1
[sc-end-basic]: /_includes/examples/pwsh/7.1-Basic-Server.ps1
[sc-end-multi]: /_includes/examples/pwsh/7.2-Multiple-Listeners.ps1
[sc-end-https]: /_includes/examples/pwsh/7.3-Https.ps1
[sc-end-pipes]: /_includes/examples/pwsh/7.4-Named-Pipes.ps1
[sc-end-unix]: /_includes/examples/pwsh/7.5-Unix-Sockets.ps1
[sc-scfg-limits]: /_includes/examples/pwsh/13.1-Server-Limits.ps1
[sc-scfg-options]: /_includes/examples/pwsh/13.2-Server-Options.ps1
[sc-life-startstop]: /_includes/examples/pwsh/15.1-Start-Stop.ps1
[sc-demo-full]: /_includes/examples/pwsh/14.1-Full-Demo.ps1
