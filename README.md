<!-- markdownlint-disable-file MD041 -->
```text
██╗  ██╗███████╗███████╗████████╗██████╗ ██╗   ██╗███╗   ██╗
██║ ██╔╝██╔════╝██╔════╝╚══██╔══╝██╔══██╗██║   ██║████╗  ██║
█████╔╝ █████╗  ███████╗   ██║   ██████╔╝██║   ██║██╔██╗ ██║
██╔═██╗ ██╔══╝  ╚════██║   ██║   ██╔██╗  ██║   ██║██║╚██╗██║
██║  ██╗███████╗███████║   ██║   ██║ ██╗ ╚██████╔╝██║ ╚████║
╚═╝  ╚═╝╚══════╝╚══════╝   ╚═╝   ╚═╝ ╚═╝  ╚═════╝ ╚═╝  ╚═══╝
Kestrun — PowerShell brains. Kestrel speed.
```

---
<!-- CI / Quality -->
[![CI](https://github.com/Kestrun/Kestrun/actions/workflows/ci-main.yml/badge.svg)](https://github.com/Kestrun/Kestrun/actions/workflows/ci-main.yml)
[![CodeQL](https://github.com/kestrun/kestrun/actions/workflows/codeql.yml/badge.svg)](https://github.com/kestrun/kestrun/actions/workflows/codeql.yml)
[![ClamAV Scan](https://img.shields.io/github/actions/workflow/status/kestrun/kestrun/clam-av-main.yml?branch=main&label=ClamAV%20Scan)](https://github.com/kestrun/kestrun/actions/workflows/clam-av-main.yml)

<!-- Coverage -->
[![CodeFactor](https://www.codefactor.io/repository/github/kestrun/kestrun/badge/main)](https://www.codefactor.io/repository/github/kestrun/kestrun/overview/main)
[![Coveralls Badge](https://coveralls.io/repos/github/Kestrun/Kestrun/badge.svg?branch=main)](https://coveralls.io/github/Kestrun/Kestrun?branch=main)
[![Coverage Status](https://coverage.kestrun.dev/badge_combined.svg)](https://coverage.kestrun.dev)

<!-- Platforms -->
[![Linux](https://img.shields.io/badge/Linux-Supported-success?logo=linux)](https://github.com/Kestrun/Kestrun/actions)
[![macOS](https://img.shields.io/badge/macOS-Supported-success?logo=apple)](https://github.com/Kestrun/Kestrun/actions)
[![Windows](https://img.shields.io/badge/Windows-Supported-success?logo=windows)](https://github.com/Kestrun/Kestrun/actions)

[![x64](https://img.shields.io/badge/x64-Supported-success?logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+PHJlY3QgeD0iNiIgeT0iNiIgd2lkdGg9IjEyIiBoZWlnaHQ9IjEyIiByeD0iMiIgZmlsbD0id2hpdGUiLz48cmVjdCB4PSIxIiB5PSI4IiB3aWR0aD0iNCIgaGVpZ2h0PSIyIiBmaWxsPSJ3aGl0ZSIvPjxyZWN0IHg9IjEiIHk9IjE0IiB3aWR0aD0iNCIgaGVpZ2h0PSIyIiBmaWxsPSJ3aGl0ZSIvPjxyZWN0IHg9IjE5IiB5PSI4IiB3aWR0aD0iNCIgaGVpZ2h0PSIyIiBmaWxsPSJ3aGl0ZSIvPjxyZWN0IHg9IjE5IiB5PSIxNCIgd2lkdGg9IjQiIGhlaWdodD0iMiIgZmlsbD0id2hpdGUiLz48L3N2Zz4=)](https://github.com/Kestrun/Kestrun/actions)
[![Arm64](https://img.shields.io/badge/Arm64-Supported-success?logo=arm)](https://github.com/Kestrun/Kestrun/actions)
<!-- Runtime -->
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![PowerShell 7.4](https://img.shields.io/badge/PowerShell-7.4-blue)](https://github.com/PowerShell/PowerShell/releases/tag/v7.4.0)
[![PowerShell 7.5](https://img.shields.io/badge/PowerShell-7.5-blue)](https://github.com/PowerShell/PowerShell/releases/tag/v7.5.0)
[![PowerShell 7.6](https://img.shields.io/badge/PowerShell-7.6-blue)](https://github.com/PowerShell/PowerShell/releases/tag/v7.6.0)

![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)
[![Contributions](https://img.shields.io/badge/contributions-welcome-brightgreen.svg)](CONTRIBUTING.md)
[![Releases](https://img.shields.io/github/v/release/Kestrun/Kestrun?display_name=release)](https://github.com/Kestrun/Kestrun/releases)
<!-- Distribution -->
[![Docs](https://img.shields.io/badge/docs-online-blue)](https://docs.kestrun.dev)
[![NuGet](https://img.shields.io/nuget/v/Kestrun)](https://www.nuget.org/packages/Kestrun/)
[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/Kestrun)](https://www.powershellgallery.com/packages/Kestrun)

#### Kestrun is a hybrid web framework that combines the speed of ASP.NET Core (Kestrel) with the flexibility of PowerShell

#### Build APIs, automation endpoints, and dynamic services using both C# and PowerShell in a single, integrated environment

## Rich Documentation and Tutorial

Full documentation and tutorial for Kestrun is available online at [docs.kestrun.dev](https://docs.kestrun.dev).
You can find guides, API references, and usage examples to help you get started and explore advanced features.

## Core Capabilities

- **🚀 Fast, cross-platform web server**
  Powered by **ASP.NET Core (Kestrel)** with full access to advanced HTTP/2, header compression, and TLS options.

- **🐚 Native PowerShell integration**
  Routes can be backed by PowerShell scripts with isolated, pooled **runspaces** and dynamic
  `$Context.Request` / `$Context.Response` variables.

- **🧠 Multi-language route execution**
  Register endpoints backed by PowerShell, C# (Roslyn scripting), VB.NET (Roslyn scripting), or native C# handlers.
  (F# / Python / JavaScript exist as language identifiers, but scripting for them is not enabled yet.)

- **🔐 Authentication built-ins**
  JWT bearer, API keys, Basic, Windows/Negotiate, and client certificate authentication.

- **📘 OpenAPI + interactive docs**
  Generate OpenAPI (v3.0 / v3.1 / v3.2) and serve docs UIs (Swagger UI / ReDoc / Scalar / RapiDoc / Elements).

- **🤖 MCP server**
  Expose route discovery, OpenAPI inspection, runtime inspection, request validation, and gated safe route invocation to MCP-compatible clients with `Kestrun.Mcp`.

- **🔁 Realtime**
  Server-Sent Events (SSE) and SignalR support.

- **🌍 Localization**
  Request-based culture resolution with PowerShell `.psd1` or JSON string tables, per-key fallback across culture hierarchies,
  and seamless integration with PowerShell routes and Razor pages.

- **🛡️ Middleware & extensibility**
  Add middleware for antiforgery, CORS, compression, caching, sessions, status code pages, exception handling, static files, gRPC, and more.

- **⚡ Task Scheduling & Background Jobs**
  Cron-based scheduling (Cronos) with PowerShell, C#, and VB.NET jobs.

## Requirements

**For Building:**

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) **AND**
  [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
  (solution multi-targets net8.0 + net10.0; net9.0 available via `-Frameworks` parameter)
- **PowerShell 7.4+** (7.4 / 7.5 run on .NET 8; 7.6 runs on .NET 10)

**Invoke-Build** and **Pester** PowerShell modules:

```powershell
Install-PSResource -Name 'InvokeBuild','Pester' -Scope CurrentUser
```

**For Runtime (Run Only):**

If you're only *running* Kestrun apps (not building from source), install the ASP.NET Core Runtime
matching the PowerShell version you are using:

| PowerShell Version | Install (Run-only) | Rationale |
|--------------------|--------------------|-----------|
| 7.4 / 7.5 | [.NET 8 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) | Bundles Microsoft.NETCore.App + Microsoft.AspNetCore.App 8.x |
| 7.6 (rc) | [.NET 10 ASP.NET Core Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) | Runtime aligning with PS 7.6 build |

Installing the **.NET SDK** already gives you the corresponding runtimes. For run-only scenarios the
**ASP.NET Core Runtime** alone is sufficient (it includes the base .NET runtime).

Download PowerShell from the official
[PowerShell Releases](https://github.com/PowerShell/PowerShell/releases).

### Verify installation

  ```powershell
  # List SDKs (expect 8.x and 9.x if building from source)
  dotnet --list-sdks

  # List runtimes (look for Microsoft.NETCore.App and Microsoft.AspNetCore.App)
  dotnet --list-runtimes | Where-Object { $_ -match 'Microsoft.(AspNetCore|NETCore).App' }
  ```

  Expected (abbreviated):

  ```text
  Microsoft.NETCore.App 8.0.x
  Microsoft.AspNetCore.App 8.0.x
  Microsoft.NETCore.App 9.0.x
  Microsoft.AspNetCore.App 9.0.x
  Microsoft.NETCore.App 10.0.x
  Microsoft.AspNetCore.App 10.0.x
  ```

  If something is missing, install the matching ASP.NET Core Runtime from the download links above.

### Build & Run

  Clone the repository:

  ```powershell
  git clone https://github.com/Kestrun/Kestrun.git
  cd Kestrun
  ```

  Build the core project and sync the PowerShell module:

  ```powershell
  Invoke-Build Restore
  Invoke-Build Build
  ```

  Build the MCP host explicitly when you want the stdio server:

  ```powershell
  Invoke-Build Build-KestrunMcp
  ```

  For MCP setup and client configuration, see [docs/guides/mcp.md](docs/guides/mcp.md).

- **🛠️ CI/CD ready**
  - Build- and run-time configurable
  - Works in containerized / headless environments
  - Supports Dev/Prod fallback module path detection

- **🛡️ Optional Add-ons**
  Add via fluent extensions:
  - `AddAntiforgery()` middleware
  - `AddStaticFiles()`, `AddDefaultFiles()`, `AddFileServer()`
  - `AddCors(policy)` or `AddCorsAllowAll()`
  - `AddSignalR<T>()` for real-time hubs
  - `AddAuthentication()` with multiple schemes (Windows, Basic, JWT, Certificate, etc.)
  - Ready for Swagger, gRPC, custom middleware hooks

- **⚡ Task Scheduling & Background Jobs**
  - **Cron-based scheduling**: Full cron expression support via Cronos
  - **Multi-language job support**: Schedule PowerShell, C#, and VB.NET scripts as background jobs
  - **Job management**: Start, stop, and monitor scheduled tasks with detailed logging

## MCP Server

Kestrun includes a standalone stdio MCP host project at `src/CSharp/Kestrun.Mcp/`.
It lets MCP-compatible clients connect to a local Kestrun script and use tools for:

- route discovery
- route metadata lookup
- generated OpenAPI retrieval
- runtime inspection
- request validation
- gated safe route invocation

Use the dedicated build task:

```powershell
Invoke-Build Build-KestrunMcp
```

Then configure your MCP client to launch `Kestrun.Mcp` with a target script and optional `--allow-invoke` route globs.
See the full guide: [docs/guides/mcp.md](docs/guides/mcp.md).

## Deployment & Extensibility

<!-- Deployment & Extensibility content intentionally follows earlier build section -->
This section summarizes extension capabilities (see earlier sections for build & run instructions).

### Using the PowerShell Module

Import the module (from source):

```powershell
Import-Module ./src/PowerShell/Kestrun/Kestrun.psm1
```

### Service Packaging Quick Start (Service/Daemon)

For full production deployment guidance, see [Production Deployment (Service/Daemon)](docs/guides/production-service-daemon.md).

Create a `Service.psd1` descriptor:

```powershell
New-KrServiceDescriptor `
  -Path .\MyServiceApp\Service.psd1 `
  -Name 'my-service' `
  -Description 'Production Kestrun service' `
  -Version 1.2.0 `
  -EntryPoint '.\Service.ps1' `
  -ServiceLogPath '.\logs\service.log' `
  -PreservePaths @('config/production.json') `
  -ApplicationDataFolders @('data/', 'logs/')
```

Package a single script (auto-generates `Service.psd1`):

```powershell
New-KrServicePackage `
  -ScriptPath .\Service.ps1 `
  -Name 'my-service' `
  -Description 'Production Kestrun service' `
  -Version 1.2.0 `
  -OutputPath .\my-service-1.2.0.krpack
```

## Running Tests

### Using Invoke-Build (Recommended)

The project includes an Invoke-Build script that automatically handles both C# (xUnit) and PowerShell (Pester) tests:

```powershell
Invoke-Build Test
```

### Manual Test Execution

#### C# Tests

```powershell
Invoke-Build Test-xUnit
```

#### PowerShell Tests

```powershell
Invoke-Build Test-Pester
```

## Documentation and Tutorial

Kestrun docs are built with [Just-the-Docs](https://github.com/just-the-docs/just-the-docs).
All new documentation **must be compatible** (front matter, `parent`, `nav_order`, etc.).

See [docs/](docs/) for structure.

## Project Structure

- `src/CSharp/` — C# core libraries and web server
  - `Kestrun.Mcp` — stdio MCP host for route, OpenAPI, runtime, and safe invocation tools
  - `Kestrun/Authentication` — authentication handlers and schemes
  - `Kestrun/Certificates` — certificate management utilities
  - `Kestrun/Hosting` — host configuration and extensions
  - `Kestrun/Languages` — multi-language scripting support (C#, VB.NET, etc.)
  - `Kestrun/Logging` — Serilog integration and logging helpers
  - `Kestrun/Middleware` — custom middleware components
  - `Kestrun/Models` — request/response classes and data models
  - `Kestrun/Razor` — Razor Pages integration with PowerShell
  - `Kestrun/Scheduling` — task scheduling and background job support
  - `Kestrun/Scripting` — script execution and validation
  - `Kestrun/Security` — security utilities and helpers
  - `Kestrun/SharedState` — thread-safe global state management
  - `Kestrun/Utilities` — shared utility functions
- `src/PowerShell/` — PowerShell module and scripts
- `examples/` — Example projects and demonstrations
  - `CSharp/Authentication` — authentication examples
  - `CSharp/Certificates` — certificate usage examples
  - `CSharp/HtmlTemplate` — HTML templating examples
  - `CSharp/MultiRoutes` — multi-route examples
  - `CSharp/RazorSample` — Razor Pages examples
  - `CSharp/Scheduling` — task scheduling examples
  - `CSharp/SharedState` — shared state examples
  - `PowerShell/` — PowerShell examples
    - `BikeRentalShop/Synchronized/` — package-ready rental API using a shared in-memory state object with serialized write/persist operations
    - `BikeRentalShop/Concurrent/` — package-ready rental API using a fully concurrent dictionary-backed in-memory database
  - `Files/` — test files and resources

The two bike rental PowerShell samples intentionally show different state models under
`docs/_includes/examples/pwsh/BikeRentalShop/`. Use `Synchronized` when you want a simpler shared-state
pattern around a familiar PowerShell object graph, and `Concurrent` when you want the
in-memory database itself to be keyed with concurrent dictionaries end to end.

- `tests/` — Test projects (C#, PowerShell)
- `docs/` — Documentation files (Just-the-Docs)
- `Utility/` — Build and maintenance scripts
- `.github/` — GitHub Actions workflows
- `Lint/` — Code analysis rules

## Contributing

Contributions of all sizes are welcome — from docs improvements to new feature modules.
See [CONTRIBUTING.md](CONTRIBUTING.md) and the online guide at <https://docs.kestrun.dev/contributing>.

## License

Licensed under the MIT License. See [LICENSE](LICENSE).

## Acknowledgements

### Documentation

- [Just-the-Docs](https://github.com/just-the-docs/just-the-docs) for documentation

### Code Quality

- [ReportGenerator](https://github.com/danielpalme/ReportGenerator) for code coverage reporting
- [codefactor](https://www.codefactor.io/) for code quality analysis
- [coveralls](https://coveralls.io/) for code coverage tracking

### AI Assistance

- [ChatGPT](https://openai.com/chatgpt) for conversational AI support
- [Copilot](https://github.com/features/copilot) for AI-powered code suggestions

### Collaboration and Version Control

- [GitHub](https://github.com/) for version control and collaboration
- [NuGet](https://www.nuget.org/) for package management
- [PowerShell Gallery](https://www.powershellgallery.com/) for PowerShell modules

### Scripting and Automation

- [PowerShell](https://github.com/PowerShell/PowerShell) for scripting and automation
- [.NET](https://dotnet.microsoft.com/) for the underlying framework
- [Pester](https://pester.dev/) for PowerShell testing

### Logging and Serialization

- [PoShLog](https://github.com/PoShLog/PoShLog) — inspiration and portions of code for logging (MIT License)
- [powershell-yaml](https://github.com/cloudbase/powershell-yaml) — adapted tests and implementation code for YAML serialization (Apache-2.0 License)

---

### Using Kestrun in your projects? Support its development [![☕ Buy Me a Coffee](https://img.shields.io/badge/☕%20Buy%20Me%20a%20Coffee-Kestrun-FFDD00)](https://buymeacoffee.com/kestrun)
