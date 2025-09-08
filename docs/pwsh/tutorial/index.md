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

| Order | Topic | Script / Chapter | Focus |
|-------|-------|------------------|-------|
| 1 | Hello World | [1-Hello-World.ps1][1-Hello-World.ps1] | Minimal server + 1 text route |
| 2 | Content Types | [2-Multiple-Content-Types.ps1][2-Multiple-Content-Types.ps1] | JSON / XML / YAML helpers |
| 3 | Multi-Language | [3-Multi-Language-Routes.ps1][3-Multi-Language-Routes.ps1] | C#/VB inline handlers |
| 4 | Route Options | [4-Route-Options.ps1][4-Route-Options.ps1] | MapRouteOptions variations |
| 5 | Static Files | (Chapter: Static Routes) | Host static content / wwwroot |
| 6 | File Server | (Chapter: File Server) | Directory browsing & file responses |
| 7 | Override Routes | (Chapter: Static Override) | Replace static with dynamic |
| 8 | FavIcon | (Chapter: Add FavIcon) | Serve favicon.ico |
| 9 | Shared State | (Chapter: Shared Variables) | Share state between routes |
| 10 | Logging Basics | (Chapter: Simple Logging) | Console logger, levels |
| 11 | Logging Advanced | (Chapter: Multiple Loggers) | Multiple sinks & levels |
| 12 | Logging Enrichment | (Chapter: Enrichment / Correlation) | Correlation IDs |
| 13 | Logging Sinks | (Chapter: Sinks) | Built-in sinks overview |
| 14 | Logging Sinks Advanced | (Chapter: Sinks Advanced) | Custom/advanced sinks |
| 15 | Logging Hot Reload | (Chapter: Hot Reload) | Reconfigure without restart |
| 16 | Certificates: Self-Signed | [6.1-Cert-SelfSigned.ps1][6.1-Cert-SelfSigned.ps1] | Dev cert generation |
| 17 | Certificates: CSR | [6.2-Cert-CSR.ps1][6.2-Cert-CSR.ps1] | Create signing request |
| 18 | Certificates: Import/Export | [6.3-Cert-Import-Export.ps1][6.3-Cert-Import-Export.ps1] | Manage PFX lifecycle |
| 19 | Endpoints: Basic Server | (Chapter: Basic Server) | Minimal hosting |
| 20 | Endpoints: Multiple Listeners | (Chapter: Multiple Listeners) | Several ports |
| 21 | Endpoints: HTTPS | (Chapter: HTTPS & Certificates) | TLS listener |
| 22 | Endpoints: Named Pipes | (Chapter: Named Pipes) | Windows IPC |
| 23 | Endpoints: Unix Sockets | (Chapter: Unix Sockets) | Unix IPC / proxy |
| 24 | Server Configuration: Limits | (Chapter: Server Limits) | Size & rate limits |
| 25 | Server Configuration: Options | (Chapter: Server Options) | Headers & runspaces |
| 26 | Lifecycle: Start/Stop | (Chapter: Start/Stop Patterns) | Non-blocking lifecycle |
| 27 | Demo: Full Server | (Chapter: Full Server Demo) | Integrated scenario |

Static chapters may be found under their respective tutorial subfolders; some entries above reference only chapter names when no direct script exists.

[1-Hello-World.ps1]: https://github.com/Kestrun/Kestrun/blob/main/examples/PowerShell/Tutorial/1-Hello-World.ps1
[2-Multiple-Content-Types.ps1]: https://github.com/Kestrun/Kestrun/blob/main/examples/PowerShell/Tutorial/2-Multiple-Content-Types.ps1
[3-Multi-Language-Routes.ps1]: https://github.com/Kestrun/Kestrun/blob/main/examples/PowerShell/Tutorial/3-Multi-Language-Routes.ps1
[4-Route-Options.ps1]: https://github.com/Kestrun/Kestrun/blob/main/examples/PowerShell/Tutorial/4-Route-Options.ps1
[6.1-Cert-SelfSigned.ps1]: https://github.com/Kestrun/Kestrun/blob/main/examples/PowerShell/Tutorial/6.1-Cert-SelfSigned.ps1
[6.2-Cert-CSR.ps1]: https://github.com/Kestrun/Kestrun/blob/main/examples/PowerShell/Tutorial/6.2-Cert-CSR.ps1
[6.3-Cert-Import-Export.ps1]: https://github.com/Kestrun/Kestrun/blob/main/examples/PowerShell/Tutorial/6.3-Cert-Import-Export.ps1
