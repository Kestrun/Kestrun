# Kestrun AI Coding Agent Instructions

## Project Overview

Kestrun is a hybrid web framework combining ASP.NET Core (Kestrel) with PowerShell scripting capabilities. It supports multi-language route execution (PowerShell, C#, VB.NET) with isolated runspace pools, comprehensive authentication, and task scheduling.

## Core Architecture Patterns

### 1. Dual Implementation Strategy
- **C# Core** (`src/CSharp/Kestrun/`): High-performance ASP.NET Core host with extensible middleware
- **PowerShell Module** (`src/PowerShell/Kestrun/`): Fluent cmdlet API wrapping C# functionality
- Both share the same `Kestrun.dll` compiled library in `lib/net8.0/` and `lib/net9.0/`

### 2. Multi-Target Framework Build
```xml
<TargetFrameworks>net8.0;net9.0</TargetFrameworks>
```
- PowerShell 7.4/7.5 → .NET 8.0
- PowerShell 7.6+ → .NET 9.0
- Conditional package references per framework

### 3. Script Engine Integration
```csharp
// Core pattern: Context injection for all script languages
server.AddMapRoute("/route", HttpVerb.Get, "Write-KrJsonResponse @{msg='Hello'}", ScriptLanguage.PowerShell);
server.AddMapRoute("/route", HttpVerb.Get, "Context.Response.WriteJsonResponse(new{msg='Hello'});", ScriptLanguage.CSharp);
```
- `$Context` variable auto-injected in PowerShell runspaces
- `Context` variable available in C#/VB.NET scripts via Roslyn compilation

## Essential Development Workflows

### Build Commands
```powershell
# Primary build workflow
Invoke-Build All                    # Clean, Restore, Build, Test
Invoke-Build Build                  # Build only - REQUIRED after any C# code changes
Invoke-Build Test                   # Run both C# (xUnit) and PowerShell (Pester) tests
Invoke-Build Package               # Release build with NuGet packaging

# Sync PowerShell module with latest compiled DLLs
Invoke-Build SyncPowerShellDll     # Copies from bin/ to src/PowerShell/Kestrun/lib/
```

**CRITICAL**: Always use `Invoke-Build Build` (not `dotnet build`) when C# code changes. This ensures proper DLL synchronization between the C# library and PowerShell module.

### Task System Integration
- VS Code tasks available for building individual example projects
- Use `run_task` tool with IDs like `"build MultiRoutes"`, `"build Authentication"`
- All tasks use `dotnet build` with solution-relative paths

### Testing Strategy
```powershell
# C# tests via xUnit
Invoke-Build Test-xUnit

# PowerShell tests via Pester
Invoke-Build Test-Pester
```
If the test only involves C# code, you can use `dotnet test` for faster execution.

## Project-Specific Conventions

### 1. PowerShell Cmdlet Patterns
All cmdlets use `Kr` prefix with fluent chaining:
```powershell
New-KrServer -Name 'MyServer' |
    Add-KrEndpoint -Port 5000 |
    Enable-KrConfiguration
```

### 2. Route Registration Patterns
**PowerShell Style (Fluent):**
```powershell
Add-KrMapRoute -Verbs Get -Pattern '/api/{id}' -ScriptBlock {
    $id = Get-KrRequestRouteParam -Name 'id'
    Write-KrJsonResponse @{id=$id} -StatusCode 200
}
```

**C# Style (Method Overloads):**
```csharp
server.AddMapRoute("/api/{id}", HttpVerb.Get, "Context.Response.WriteJsonResponse(new{id=RouteData.Values['id']});", ScriptLanguage.CSharp);
```

### 3. Logging Integration
Kestrun uses Serilog with structured logging patterns:
```powershell
$logger = New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkFile -Path './logs/app.log' -RollingInterval Hour |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'DefaultLogger' -PassThru -SetAsDefault
```

### 4. Shared State Management
Thread-safe global variables across runspaces and languages:
```powershell
Set-KrSharedState -Name 'Visits' -Value @{Count = 0}
# Available in all routes as $Visits
```

## Critical Integration Points

### 1. Runspace Pool Management
- `KestrunRunspacePoolManager` handles PowerShell script isolation
- Auto-imports modules, shared variables injected per-request
- Scripts run in separate runspaces with `$Context` pre-loaded

### 2. Multi-Language Support
- **PowerShell**: Direct runspace execution with cmdlet helpers
- **C#/VB.NET**: Roslyn compilation with ASP.NET Core context binding
- **Native C#**: Compiled delegates for maximum performance

### 3. Response Helper Patterns
Consistent response methods across languages:
```powershell
Write-KrJsonResponse, Write-KrXmlResponse, Write-KrYamlResponse
Write-KrTextResponse, Write-KrHtmlResponse, Write-KrFileResponse
```

```csharp
Context.Response.WriteJsonResponse(), Context.Response.WriteXmlResponse()
Context.Response.WriteFileResponse(), Context.Response.WriteTextResponse()
```

## File Organization Essentials

### Key Directories
- `src/CSharp/Kestrun/Hosting/` - Core host and configuration
- `src/CSharp/Kestrun/Scripting/` - Multi-language script execution
- `src/PowerShell/Kestrun/Public/` - PowerShell cmdlet implementations
- `examples/CSharp/` - C# usage examples
- `examples/PowerShell/` - PowerShell usage examples
- `Utility/` - Build and maintenance PowerShell scripts

### Configuration Files
- `global.json` - .NET SDK version pinning
- `Kestrun.build.ps1` - Invoke-Build script with all tasks
- `version.json` - Version management for builds
- Tasks defined in `.vscode/tasks.json` for individual example builds

## Common Patterns to Follow

When adding new features:
1. **Implement C# core first** in appropriate namespace under `src/CSharp/Kestrun/`
2. **Create PowerShell wrapper** with `Kr` prefix in `src/PowerShell/Kestrun/Public/`
3. **Add examples** in both `examples/CSharp/` and `examples/PowerShell/`
4. **Update tests** in both `tests/CSharp.Tests/` and `tests/PowerShell.Tests/`
5. **Document usage** in `docs/` following Just-the-Docs structure

When debugging:
- Use `Get-KrRoot` to resolve relative paths consistently
- Check runspace pool status for PowerShell execution issues
- Verify DLL sync between `bin/` and `src/PowerShell/Kestrun/lib/` directories



## 🌿 Branch & Commit Naming Rules

### Branch Naming Convention

```text
<type>-<issue-number>-<short-kebab-case-description>
```

- **type**:

  - `feat` → new feature
  - `fix` → bug fix
  - `refactor` → restructuring/cleanup
  - `docs` → documentation changes
  - `test` → test-only changes
  - `chore` → build, CI, tooling, infra
  - `techdebt` → explicit technical debt

- **issue-number**: GitHub issue or PR number (if applicable).

- **short-description**: brief, lowercase, hyphenated summary.

**Examples:**

- `feat-42-add-jwt-auth`
- `fix-103-csrf-validation`
- `refactor-57-reduce-complexity`
- `docs-88-update-readme-badges`

---

### Commit Message Convention (Conventional Commits)

Use the format:

```text
<type>(scope?): <short summary>
```

- **type**: same as branch types (`feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `techdebt`)
- **scope** (optional): module, cmdlet, or subsystem (e.g., `auth`, `host`, `ci`).
- **summary**: imperative, ≤ 72 chars, no period at end.

**Examples:**

- `feat(auth): add cookie authentication support`
- `fix(host): correct IPv6 URI parsing`
- `refactor(core): split large ConfigureListener function`
- `docs(ci): update workflow badges in README`

---

### PR Titles

- Mirror commit style for consistency.
- Reference the issue number with `Fixes #NN` or `Refs #NN`.

**Examples:**

- `refactor(core): reduce function complexity (Fixes #57)`
- `feat(auth): add support for JWT bearer tokens (Refs #42)`

## ✅ Pull Request Checklist

Before submitting your PR, please confirm you’ve covered the essentials:

- [ ] **Branch name** follows convention:
  `<type>/<issue-number>-<short-description>`
  *(e.g., `refactor/57-reduce-complexity`)*

- [ ] **Commit messages** follow [Conventional Commits](https://www.conventionalcommits.org/) (`type(scope): summary`).

- [ ] **Build passes locally**:

  ```powershell
  Invoke-Build Restore ; Invoke-Build Build
  ```

- [ ] **Tests pass locally**:

  ```powershell
  Invoke-Build Test
  ```

- [ ] **New/changed behavior covered by tests** (xUnit for C#, Pester for PowerShell).

- [ ] **Public APIs documented**:

  - C#: XML doc comments
  - PowerShell: Comment-based help

- [ ] **Docs updated** (if user-facing):

  - Just-the-Docs compatible (front matter, nav order, sections correct).

- [ ] **Changelog entry added** (if user-facing change).

- [ ] **Linked to issue** with `Fixes #NN` or `Refs #NN`.

## 📝 Style & Quality

### C\#

- Follow Microsoft C# conventions.
- Prefer explicit types for public APIs; keep internals tidy.
- Use nullable reference types and `ConfigureAwait(false)` in library code where relevant.

### PowerShell

- Approved verbs (`Get-`, `New-`, `Add-`, `Set-`, `Remove-`, `Test-`, etc.).
- Include comment-based help for all public functions.
- Avoid global state; design for pipeline-friendliness.
- Keep cmdlets fast and predictable—pure where possible.

### Testing

- Prefer **Pester v5** tests colocated under `tests/`.
- One behavioral concern per test; name tests descriptively.
- When fixing a bug, add a failing test first.

---

## 📚 Documentation (Just-the-Docs compatible)

All docs must render cleanly with **[Just-the-Docs](https://github.com/just-the-docs/just-the-docs)** (as used by the Kestrun site).
Key rules:

- Every page requires a **front matter** block.
- Use **`parent`**, **`nav_order`**, and **`has_children`** to control navigation.
- Keep cmdlets under the **“PowerShell Cmdlets”** section; tutorials under **“Tutorials.”**

### Front Matter Templates

**Cmdlet page (example):**

```markdown
---
layout: default
parent: PowerShell Cmdlets
title: Get-KrScheduleReport
nav_order: 60
render_with_liquid: false
---

# Get-KrScheduleReport

> Short, imperative synopsis here.

## SYNOPSIS
Returns the full schedule report.

## SYNTAX

```powershell

Get-KrScheduleReport \[\[-Server] <KestrunHost>] \[\[-TimeZoneId] <String>] \[-AsHashtable]

````

## DESCRIPTION

Concise, user-focused description…

## EXAMPLES

```powershell
Get-KrScheduleReport -AsHashtable
````

## PARAMETERS

- **Server** — …
- **TimeZoneId** — …

````text

**Tutorial page (example):**
```markdown
---
layout: default
parent: Tutorials
title: Static Routes
nav_order: 3
---

# Introduction to Static Routes

A crisp overview…

## Quick start
```powershell
Invoke-Build Restore ; Invoke-Build Build
````

````text

### Navigation Tips (Just-the-Docs)
- Root landing page should be a friendly overview of features with deep links.
- Use `nav_order` to sort; lower numbers appear first.
- Use `has_children: true` on a section index page if it owns subpages.

**Section index example:**
```markdown
---
layout: default
title: PowerShell Cmdlets
nav_order: 30
has_children: true
---

# PowerShell Cmdlets

Browse the Kestrun command surface…
````

### Content Conventions

- **Headings**: Use `#`, `##`, `###` sensibly; keep titles short.
- **Callouts**: Use Markdown blockquotes:

  > **Note:** This behavior requires PowerShell 7.4+
  > **Warning:** Rotating secrets? Update appsettings too.
- **Code fences**: Use language hints (` ```powershell`, ` ```csharp`).
- **Links**: Relative links within the docs; absolute links for external sites.

---

## ✅ Pull Request Checklist Form

- [ ] Built successfully: `Invoke-Build Restore ; Invoke-Build Build`
- [ ] Tests pass: `Invoke-Build Test`
- [ ] New/changed behavior covered by Pester tests
- [ ] Public APIs documented (XML docs for C#, comment-based help for PowerShell)
- [ ] Docs are **Just-the-Docs** compliant and correctly placed (Cmdlets/Tutorials)
- [ ] Changelog entry if user-facing

---

## 🐛 Filing Issues

Please include:

- Repro steps and expected vs. actual behavior
- Versions: OS, PowerShell (must be 7.4+), .NET SDK
- Logs, stack traces, and minimal code samples
