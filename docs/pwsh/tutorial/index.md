---
title: Tutorials
parent: PowerShell

nav_order: 0
---

# Tutorials

Step-by-step guides to build and ship with Kestrun. This index lists runnable sample scripts and documentation chapters in recommended learning order.

## Prerequisites

- PowerShell 7.4, 7.5, or 7.6 (preview)
- .NET (run-only scenarios do NOT require the full SDK)

| PowerShell Version | Install (Run Samples) | Notes |
|--------------------|-----------------------|-------|
| 7.4 / 7.5 | [.NET 8 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) | Bundles base runtime + ASP.NET Core |
| 7.6 (preview) | [.NET 9 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) | Preview – updates frequently |

If you already have the **.NET SDK** for those versions installed you don't need to install the runtime separately.

- Kestrun module: installed or available from this repository at `src/PowerShell/Kestrun/Kestrun.psm1`
- Supported OS: same as .NET 8/9 (Windows, Linux, macOS), including ARM/ARM64

Verify (optional):

```powershell
dotnet --list-runtimes | Where-Object { $_ -match 'Microsoft.(AspNetCore|NETCore).App' }
```

You should see `Microsoft.AspNetCore.App 8.0.x` (and 9.0.x if using PS 7.6 preview).

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

| Order | Topic                         | Chapter                                   | Sample Script                   | Focus                               |
| ----- | ----------------------------- | ----------------------------------------- | ------------------------------- | ----------------------------------- |
| 1     | Hello World                   | [Hello World][ch-hello]                   | [Script][sc-hello]              | Minimal server + 1 text route       |
| 2     | Content Types                 | [Content Types][ch-content]               | [Script][sc-content]            | JSON / XML / YAML helpers           |
| 3     | Multi-Language                | [Multi-Language Routes][ch-multilang]     | [Script][sc-multilang]          | C#/VB inline handlers               |
| 4     | Route Options                 | [Route Options][ch-routeopts]             | [Script][sc-routeopts]          | MapRouteOptions variations          |
| 5     | Static Files                  | [Static Routes][ch-static]                | [Script][sc-static]             | Host static content / wwwroot       |
| 6     | File Server                   | [File Server][ch-fileserver]              | [Script][sc-fileserver]         | Directory browsing & file responses |
| 7     | Override Routes               | [Override Routes][ch-override]            | [Script][sc-override]           | Replace static with dynamic         |
| 8     | FavIcon                       | [Add FavIcon][ch-favicon]                 | [Script][sc-favicon]            | Serve favicon.ico                   |
| 9     | Shared State                  | [Shared Variables][ch-shared]             | [Script][sc-shared]             | Share state between routes          |
| 10    | Logging Basics                | [Simple Logging][ch-log-basic]            | [Script][sc-log-basic]          | Console logger, levels              |
| 11    | Logging Advanced              | [Multiple Loggers][ch-log-adv]            | [Script][sc-log-adv]            | Multiple sinks & levels             |
| 12    | Logging Enrichment            | [Enrichment & Correlation][ch-log-enr]    | [Script][sc-log-enr]            | Correlation IDs                     |
| 13    | Logging Sinks                 | [Sinks][ch-log-sinks]                     | [Script][sc-log-sinks]          | Built-in sinks overview             |
| 14    | Logging Sinks Advanced        | [Sinks Advanced][ch-log-sinks-adv]        | [Script][sc-log-sinks-adv]      | Custom/advanced sinks               |
| 15    | Logging Hot Reload            | [Hot Reload][ch-log-reload]               | [Script][sc-log-reload]         | Reconfigure without restart         |
| 16    | Certificates: Self-Signed     | [Self-Signed][ch-cert-self]               | [Script][sc-cert-self]          | Dev cert generation                 |
| 17    | Certificates: CSR             | [CSR][ch-cert-csr]                        | [Script][sc-cert-csr]           | Create signing request              |
| 18    | Certificates: Import/Export   | [Import / Export][ch-cert-imp-exp]        | [Script][sc-cert-imp-exp]       | Manage PFX lifecycle                |
| 19    | Endpoints: Basic Server       | [Basic Server][ch-end-basic]              | [Script][sc-end-basic]          | Minimal hosting                     |
| 20    | Endpoints: Multiple Listeners | [Multiple Listeners][ch-end-multi]        | [Script][sc-end-multi]          | Several ports                       |
| 21    | Endpoints: HTTPS              | [HTTPS][ch-end-https]                     | [Script][sc-end-https]          | TLS listener                        |
| 22    | Endpoints: Named Pipes        | [Named Pipes][ch-end-pipes]               | [Script][sc-end-pipes]          | Windows IPC                         |
| 23    | Endpoints: Unix Sockets       | [Unix Sockets][ch-end-unix]               | [Script][sc-end-unix]           | Unix IPC / proxy                    |
| 24    | Responses: Text & JSON        | [Text & JSON][ch-resp-text-json]          | [Script][sc-resp-1]             | Basic text & JSON helpers           |
| 25    | Responses: Structured         | [Structured Formats][ch-resp-structured]  | [Script][sc-resp-2]             | XML / YAML / CSV                    |
| 26    | Responses: Binary & Stream    | [Binary & Stream][ch-resp-binary]         | [Script][sc-resp-3]             | Binary bytes vs streaming           |
| 27    | Responses: HTML & Files       | [HTML & Files][ch-resp-html]              | [Script][sc-resp-4]             | Templates & file download           |
| 28    | Responses: BSON / CBOR        | [BSON / CBOR][ch-resp-special]            | [Script][sc-resp-5]             | Compact binary encodings            |
| 29    | Responses: Redirects          | [Redirects][ch-resp-redirects]            | [Script][sc-resp-6]             | 3xx location responses              |
| 30    | Responses: Errors             | [Errors][ch-resp-errors]                  | [Script][sc-resp-7]             | Consistent error payloads           |
| 31    | Responses: Caching            | [Caching & Revalidation][ch-resp-caching] | [Script][sc-resp-8]             | Cache-Control & ETag handling       |
| 32    | Responses: Content Negotiation| [Content Negotiation][ch-resp-negotiation]| [Script][sc-resp-9]             | Auto format selection via Accept   |
| 33    | Responses: Low-Level Stream   | [Low-Level Response][ch-resp-lowlevel]    | [Script][sc-resp-10]            | Raw stream writing                  |
| 34    | Middleware: Antiforgery       | [Antiforgery Protection][ch-mw-antiforgery] | [Script][sc-mw-antiforgery]   | CSRF token + cookie validation      |
| 35    | Authentication: Basic (PS)    | [Basic (PS)][ch-auth-basic-ps]            | [Script][sc-auth-basic-ps]      | Basic scheme script block           |
| 36    | Authentication: Basic (C#/VB) | [Basic (C#/VB)][ch-auth-basic-multi]      | [Script][sc-auth-basic-multi]   | Multi-language validation           |
| 37    | Authentication: API Key       | [API Key][ch-auth-apikey]                 | [Script][sc-auth-apikey]        | Header key variants                 |
| 38    | Authentication: JWT           | [JWT Tokens][ch-auth-jwt]                 | [Script][sc-auth-jwt]           | Issue & validate bearer             |
| 39    | Authentication: Cookies       | [Cookies][ch-auth-cookies]                | [Script][sc-auth-cookies]       | Session cookie login                |
| 40    | Authentication: Windows       | [Windows Authentication][ch-auth-windows] | [Script][sc-auth-windows]       | Integrated Windows auth             |
| 41    | Authentication: Claims        | [Claims & Policies][ch-auth-claims]       | [Script][sc-auth-claims]        | Claim policy enforcement            |
| 42    | Authentication: Multiple      | [Multiple Schemes][ch-auth-multi-schemes] | [Script][sc-auth-multi-schemes] | Combine schemes                     |
| 43    | Authentication: Full Demo     | [Full Auth Demo][ch-auth-full]            | [Script][sc-auth-full]          | Integrated auth server              |
| 44    | Server Configuration: Limits  | [Server Limits][ch-scfg-limits]           | [Script][sc-scfg-limits]        | Size & rate limits                  |
| 45    | Server Configuration: Options | [Server Options][ch-scfg-options]         | [Script][sc-scfg-options]       | Headers & runspaces                 |
| 46    | Lifecycle: Start/Stop         | [Start / Stop][ch-life-startstop]         | [Script][sc-life-startstop]     | Non-blocking lifecycle              |
| 47    | Demo: Full Server             | [Full Server Demo][ch-demo-full]          | [Script][sc-demo-full]          | Integrated scenario                 |

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
[ch-auth-basic-ps]: ./8.authentication/1.Basic-PS
[ch-auth-basic-multi]: ./8.authentication/2.Basic-MultiLang
[ch-auth-apikey]: ./8.authentication/3.Api-Key
[ch-auth-jwt]: ./8.authentication/4.Jwt
[ch-auth-cookies]: ./8.authentication/5.Cookies
[ch-auth-windows]: ./8.authentication/6.Windows-Authentication
[ch-auth-claims]: ./8.authentication/7.Claims-Policies
[ch-auth-multi-schemes]: ./8.authentication/8.Multiple-Schemes
[ch-auth-full]: ./8.authentication/9.Full-Demo
[ch-resp-text-json]: ./9.Responses/1.Basic-Text-Json
[ch-resp-structured]: ./9.Responses/2.Structured-Xml-Yaml-Csv
[ch-resp-binary]: ./9.Responses/3.Binary-Stream
[ch-resp-html]: ./9.Responses/4.Html-Templates-Files
[ch-resp-special]: ./9.Responses/5.Special-Bson-Cbor
[ch-resp-redirects]: ./9.Responses/6.Redirects
[ch-resp-errors]: ./9.Responses/7.Errors
[ch-resp-caching]: ./9.Responses/8.Caching
[ch-resp-negotiation]: ./9.Responses/9.Content-Negotiation
[ch-resp-lowlevel]: ./9.Responses/10.Low-Level-Response
[ch-mw-antiforgery]: ./10.middleware/1.Antiforgery
[ch-scfg-limits]: ./13.server-configuration/1.Server-Limits
[ch-scfg-options]: ./13.server-configuration/2.Server-Options
[ch-life-startstop]: ./14.lifecycle/1.Start-Stop
[ch-demo-full]: ./15.demos/1.Full-Demo
[sc-hello]: /pwsh/tutorial/examples/1.1-Hello-World.ps1
[sc-content]: /pwsh/tutorial/examples/2.1-Multiple-Content-Types.ps1
[sc-multilang]: /pwsh/tutorial/examples/2.2-Multi-Language-Routes.ps1
[sc-routeopts]: /pwsh/tutorial/examples/2.4-Route-Options.ps1
[sc-static]: /pwsh/tutorial/examples/3.1-Static-Routes.ps1
[sc-fileserver]: /pwsh/tutorial/examples/3.2-File-Server.ps1
[sc-override]: /pwsh/tutorial/examples/3.3-Static-OverrideRoutes.ps1
[sc-favicon]: /pwsh/tutorial/examples/3.4-Add-FavIcon.ps1
[sc-shared]: /pwsh/tutorial/examples/4.1-Shared-Variables.ps1
[sc-log-basic]: /pwsh/tutorial/examples/5.1-Simple-Logging.ps1
[sc-log-adv]: /pwsh/tutorial/examples/5.2-Multiple-Loggers-Levels.ps1
[sc-log-enr]: /pwsh/tutorial/examples/5.3-Enrichment-Correlation-IDs.ps1
[sc-log-sinks]: /pwsh/tutorial/examples/5.4-Sinks.ps1
[sc-log-sinks-adv]: /pwsh/tutorial/examples/5.5-Sinks-Advanced.ps1
[sc-log-reload]: /pwsh/tutorial/examples/5.6-Hot-Reload.ps1
[sc-cert-self]: /pwsh/tutorial/examples/6.1-Cert-SelfSigned.ps1
[sc-cert-csr]: /pwsh/tutorial/examples/6.2-Cert-CSR.ps1
[sc-cert-imp-exp]: /pwsh/tutorial/examples/6.3-Cert-Import-Export.ps1
[sc-end-basic]: /pwsh/tutorial/examples/7.1-Basic-Server.ps1
[sc-end-multi]: /pwsh/tutorial/examples/7.2-Multiple-Listeners.ps1
[sc-end-https]: /pwsh/tutorial/examples/7.3-Https.ps1
[sc-end-pipes]: /pwsh/tutorial/examples/7.4-Named-Pipes.ps1
[sc-end-unix]: /pwsh/tutorial/examples/7.5-Unix-Sockets.ps1
[sc-auth-basic-ps]: /pwsh/tutorial/examples/8.1-Basic-PS.ps1
[sc-auth-basic-multi]: /pwsh/tutorial/examples/8.2-Basic-MultiLang.ps1
[sc-auth-apikey]: /pwsh/tutorial/examples/8.3-Api-Key.ps1
[sc-auth-jwt]: /pwsh/tutorial/examples/8.4-Jwt.ps1
[sc-auth-cookies]: /pwsh/tutorial/examples/8.5-Cookies.ps1
[sc-auth-windows]: /pwsh/tutorial/examples/8.6-Windows-Authentication.ps1
[sc-auth-claims]: /pwsh/tutorial/examples/8.7-Claims-Policies.ps1
[sc-auth-multi-schemes]: /pwsh/tutorial/examples/8.8-Multiple-Schemes.ps1
[sc-auth-full]: /pwsh/tutorial/examples/8.9-Full-Demo.ps1
[sc-resp-1]: /pwsh/tutorial/examples/9.1-Text-Json.ps1
[sc-resp-2]: /pwsh/tutorial/examples/9.2-Structured-Xml-Yaml-Csv.ps1
[sc-resp-3]: /pwsh/tutorial/examples/9.3-Binary-Stream.ps1
[sc-resp-4]: /pwsh/tutorial/examples/9.4-Html-Files.ps1
[sc-resp-5]: /pwsh/tutorial/examples/9.5-Bson-Cbor.ps1
[sc-resp-6]: /pwsh/tutorial/examples/9.6-Redirects.ps1
[sc-resp-7]: /pwsh/tutorial/examples/9.7-Errors.ps1
[sc-resp-8]: /pwsh/tutorial/examples/9.8-Caching.ps1
[sc-resp-9]: /pwsh/tutorial/examples/9.9-Content-Negotiation.ps1
[sc-resp-10]: /pwsh/tutorial/examples/9.10-Low-Level-Response.ps1
[sc-mw-antiforgery]: /pwsh/tutorial/examples/10.1-Antiforgery.ps1
[sc-scfg-limits]: /pwsh/tutorial/examples/13.1-Server-Limits.ps1
[sc-scfg-options]: /pwsh/tutorial/examples/13.2-Server-Options.ps1
[sc-life-startstop]: /pwsh/tutorial/examples/15.1-Start-Stop.ps1
[sc-demo-full]: /pwsh/tutorial/examples/14.1-Full-Demo.ps1
