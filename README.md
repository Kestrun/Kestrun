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

![CI](https://github.com/Kestrun/Kestrun/actions/workflows/ci.yml/badge.svg)
[![CodeQL](https://github.com/kestrun/kestrun/actions/workflows/codeql.yml/badge.svg)](https://github.com/kestrun/kestrun/actions/workflows/codeql.yml)
![ClamAV Scan](https://img.shields.io/github/actions/workflow/status/kestrun/kestrun/clam-av.yml?branch=main&label=ClamAV%20Scan)

[![CodeFactor](https://www.codefactor.io/repository/github/kestrun/kestrun/badge/main)](https://www.codefactor.io/repository/github/kestrun/kestrun/overview/main)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/1ea6075b819449a1aff2678f41f890c2)](https://app.codacy.com/gh/Kestrun/Kestrun/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)
[![Coverage Status](https://coveralls.io/repos/github/Kestrun/Kestrun/badge.svg?branch=main)](https://coveralls.io/github/Kestrun/Kestrun?branch=main)
[![Coverage Status](https://coverage.kestrun.dev/report/badge_combined.svg)](https://coverage.kestrun.dev/report) 

[![Linux](https://img.shields.io/badge/Linux-Supported-success?logo=linux)](https://github.com/Kestrun/Kestrun/actions)
[![macOS](https://img.shields.io/badge/macOS-Supported-success?logo=apple)](https://github.com/Kestrun/Kestrun/actions)
[![Windows](https://img.shields.io/badge/Windows-Supported-success?logo=windows)](https://github.com/Kestrun/Kestrun/actions)

[![x64](https://img.shields.io/badge/x64-Supported-success?logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+PHJlY3QgeD0iNiIgeT0iNiIgd2lkdGg9IjEyIiBoZWlnaHQ9IjEyIiByeD0iMiIgZmlsbD0id2hpdGUiLz48cmVjdCB4PSIxIiB5PSI4IiB3aWR0aD0iNCIgaGVpZ2h0PSIyIiBmaWxsPSJ3aGl0ZSIvPjxyZWN0IHg9IjEiIHk9IjE0IiB3aWR0aD0iNCIgaGVpZ2h0PSIyIiBmaWxsPSJ3aGl0ZSIvPjxyZWN0IHg9IjE5IiB5PSI4IiB3aWR0aD0iNCIgaGVpZ2h0PSIyIiBmaWxsPSJ3aGl0ZSIvPjxyZWN0IHg9IjE5IiB5PSIxNCIgd2lkdGg9IjQiIGhlaWdodD0iMiIgZmlsbD0id2hpdGUiLz48L3N2Zz4=)](https://github.com/Kestrun/Kestrun/actions)
[![Arm64](https://img.shields.io/badge/Arm64-Supported-success?logo=arm)](https://github.com/Kestrun/Kestrun/actions)

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![PowerShell 7.4](https://img.shields.io/badge/PowerShell-7.4-blue)](https://github.com/PowerShell/PowerShell/releases/tag/v7.4.0)
[![PowerShell 7.5](https://img.shields.io/badge/PowerShell-7.5-blue)](https://github.com/PowerShell/PowerShell/releases/tag/v7.5.0)
[![PowerShell 7.6](https://img.shields.io/badge/PowerShell-7.6(preview)-blue)](https://github.com/PowerShell/PowerShell/releases/tag/v7.6.0-preview.4)

![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)
[![Contributions](https://img.shields.io/badge/contributions-welcome-brightgreen.svg)](CONTRIBUTING.md)
[![Releases](https://img.shields.io/github/v/release/Kestrun/Kestrun?display_name=release)](https://github.com/Kestrun/Kestrun/releases)

[![Docs](https://img.shields.io/badge/docs-online-blue)](https://docs.kestrun.dev)
[![NuGet](https://img.shields.io/nuget/v/Kestrun)](https://www.nuget.org/packages/Kestrun/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Kestrun)](https://www.nuget.org/packages/Kestrun/)
[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/Kestrun)](https://www.powershellgallery.com/packages/Kestrun)

Kestrun is a hybrid web framework that combines the speed and scalability of ASP.NET Core (Kestrel) with the
flexibility and scripting power of PowerShell. It enables you to build web APIs, automation endpoints, and
dynamic services using both C# and PowerShell in a single, integrated environment.

## Rich Documentation and Tutorial

Full documentation and tutorial for Kestrun is available online at [docs.kestrun.dev](https://docs.kestrun.dev).  
You can find guides, API references, and usage examples to help you get started and explore advanced features.

## Core Capabilities

- **🚀 Fast, cross-platform web server**  
  Powered by **ASP.NET Core (Kestrel)** with full access to advanced HTTP/2, header compression, and TLS options.

- **🐚 Native PowerShell integration**  
  Routes can be backed by PowerShell scripts with isolated, pooled **runspaces** and dynamic  
  `$Context.Request` / `$Context.Response` variables.

- **🧠 Multi-language script routing**  
  Register HTTP routes using:
  - 🐚 PowerShell  
  - 🧩 C# scripts (Roslyn compiled with typed globals and shared state)  
  - 📄 VB.NET scripts (full .NET scripting with claims and validation support)  
  - 🐍 Python (via Python.NET)  
  - 📜 JavaScript (via ClearScript + V8)  
  - 🧪 F# (stubbed for future support)

- **📄 Razor Pages backed by PowerShell**  
  Use `.cshtml + .cshtml.ps1` pairs with automatic `$Model` injection and dynamic rendering via `HttpContext.Items["PageModel"]`.

- **📦 Modular architecture**  
  Combine C# libraries, PowerShell modules, Razor views, static files, and custom handlers into a unified web app.

## HTTP & Protocol Support

- **🌍 Rich HTTP support**  
  - Routes with query, headers, body support  
  - Static files with custom headers, `Content-Disposition`, stream/async send  
  - Built-in MIME type detection  
  - Charset and compression negotiation

- **🔐 TLS/HTTPS & Certificate support**  
  - Supports `X509Certificate2` objects directly  
  - Fine-grained listener control: `Protocols`, `UseConnectionLogging`, HTTP/1.1 & HTTP/2  
  - Hot-swap of certificate or listener settings

- **🛡️ Comprehensive Authentication & Authorization**  
  - **Multiple authentication schemes**: Windows, Basic, API Key, JWT Bearer, Cookie, Certificate, Negotiate, OpenID Connect
  - **Claims-based authorization**: Rich claim policies with PowerShell and VB.NET claim providers  
  - **Route-level authorization**: Fine-grained access control per endpoint  
  - **Credential validation**: Supports SecureString utilities and custom validation delegates

## Developer-Focused

- **🧪 Test-friendly architecture**  
  - **C#**: xUnit + script compilation validation (`ValidateCSharpScript`)  
  - **PowerShell**: Pester-compatible setup for route and module tests  
  - Script diagnostics: line-numbered errors, detailed exception formatting

- **🧬 Shared global state**  
  A thread-safe, case-insensitive `SharedState` store for global variables, usable across C#, PowerShell, and Razor.

- **🖨️ Flexible response output**  
  Respond with:
  - `WriteTextResponse`, `WriteJsonResponse`, `WriteXmlResponse`, `WriteYamlResponse`  
  - `WriteFileResponse`, `WriteBinaryResponse`, `WriteStreamResponse`  
  - Optional `Content-Disposition: inline` / `attachment; filename=…`

- **🧵 Thread-safe runspace pooling**  
  Automatic pooling of PowerShell runspaces with configurable min/max, affinity (`PSThreadOptions`), and module injection.

- **📑 Script validation & compilation error reporting**  
  C# route validation returns detailed Roslyn diagnostics without throwing (e.g., for editor integration or CI prechecks).

- **🧾 Logging with Serilog**  
  - Fluent `KestrunLoggerBuilder` for per-subsystem loggers  
  - Named logger registration & retrieval  
  - Reset/Reload/Dispose support for hot-reload or graceful shutdowns  
  - Default rolling file logs (`logs/kestrun.log`)

## Deployment & Extensibility

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

## Getting Started

### Prerequisites

**For Building:**

- [.NET 8 SDK](https://dotnet.microsoft.com/download) AND [.NET 9 SDK](https://dotnet.microsoft.com/download) (both required)
- **PowerShell 7.4+**
- **Invoke-Build** and **Pester** PowerShell modules:

```powershell
Install-PSResource -Name 'InvokeBuild','Pester' -Scope CurrentUser
```

**For Runtime:**

- [.NET 8 Runtime](https://dotnet.microsoft.com/download) or [.NET 9 Runtime](https://dotnet.microsoft.com/download)
- **PowerShell 7.4+** (download from [PowerShell GitHub Releases](https://github.com/PowerShell/PowerShell/releases))

### Build & Run

Clone the repository:

```powershell
git clone https://github.com/Kestrun/Kestrun.git
cd Kestrun
```

Build the solution using Invoke-Build:

```powershell
Invoke-Build Restore ; Invoke-Build Build
```

Run an example (e.g., MultiRoutes):

```powershell
dotnet run --project .\examples\CSharp\MultiRoutes\MultiRoutes.csproj
```

### Using the PowerShell Module

Import the module (from source):

```powershell
Import-Module ./src/PowerShell/Kestrun/Kestrun.psm1
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
Invoke-Build Kestrun.Tests
```

#### PowerShell Tests

```powershell
Invoke-Build Test-Pester
```

## Documentation

Kestrun docs are built with [Just-the-Docs](https://github.com/just-the-docs/just-the-docs).  
All new documentation **must be compatible** (front matter, `parent`, `nav_order`, etc.).  

See [docs/](docs/) for structure.

## Project Structure

- `src/CSharp/` — C# core libraries and web server
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
  - `Files/` — test files and resources
- `tests/` — Test projects (C#, PowerShell)
- `docs/` — Documentation files (Just-the-Docs)
- `Utility/` — Build and maintenance scripts
- `.github/` — GitHub Actions workflows
- `Lint/` — Code analysis rules

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and the online guide at <https://docs.kestrun.dev/contributing>.

## License

Licensed under the MIT License. See [LICENSE](LICENSE).
