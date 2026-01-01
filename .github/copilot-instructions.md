# Kestrun AI Coding Agent Instructions

## Project Overview

Kestrun is a hybrid web framework combining ASP.NET Core (Kestrel) with PowerShell scripting capabilities. It supports multi-language route execution (PowerShell, C#, VB.NET) with isolated runspace pools, comprehensive authentication, and task scheduling.

## Core Architecture Patterns

### 1. Dual Implementation Strategy
- **C# Core** (`src/CSharp/Kestrun/`): High-performance ASP.NET Core host with extensible middleware
- **PowerShell Module** (`src/PowerShell/Kestrun/`): Fluent cmdlet API wrapping C# functionality
- Both share the same `Kestrun.dll` compiled library in `lib/net8.0/`, `lib/net9.0/`, and `lib/net10.0/`

### 2. Multi-Target Framework Build
```xml
<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
```
- PowerShell 7.4/7.5 → .NET 8.0
- PowerShell 7.6+ → .NET 10.0
- net9.0 available via explicit `-Frameworks` parameter
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

**CRITICAL**
Follow the Contributing guidelines in the CONTRIBUTING.md file for detailed instructions on PR structure, testing, and documentation.

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


## OpenAPI (Kestrun implementation)

Kestrun can generate OpenAPI documents (v3.0 / v3.1 / v3.2) from PowerShell and C# route definitions.

### PowerShell: quick start

Minimum setup (typical example script pattern):

```powershell
# OpenAPI metadata
Add-KrOpenApiInfo -Title 'My API' -Version '1.0.0'
Add-KrOpenApiServer -Url 'http://127.0.0.1:5000' -Description 'Local'

# Expose OpenAPI JSON endpoints
Add-KrOpenApiRoute

# (Optional) Swagger / Redoc UI routes
Add-KrApiDocumentationRoute
```

Validation helpers (recommended in examples/tests):

```powershell
$doc = Build-KrOpenApiDocument
Test-KrOpenApiDocument -Document $doc
```

Common URLs when running examples:

- OpenAPI JSON: `/openapi/v3.1/openapi.json`
- Swagger UI: `/swagger`
- ReDoc: `/redoc`

### Document metadata (info/license)

- Prefer `Add-KrOpenApiInfo` for `info.title`, `info.version`, etc.
- `info.license` supports either a URL or an SPDX identifier:
  - Use `Add-KrOpenApiLicense -Name 'Apache 2.0' -Url 'https://…'` when you want a link.
  - Use `Add-KrOpenApiLicense -Name 'Apache 2.0' -Identifier 'Apache-2.0'` when you want an SPDX ID (OpenAPI 3.1+ / 3.2 use `license.identifier`).

### Servers and server variables

- Use `Add-KrOpenApiServer -Url` to populate `servers[]`.
- For templated servers (`https://{env}.api.example.com`), use variables via `New-KrOpenApiServerVariable` and pass them to `Add-KrOpenApiServer -Variables`.
- Keep server variables consistent across v3.0/v3.1/v3.2 documents when examples/tests validate all versions.

### Modeling operations

- Use `[OpenApiPath(...)]` on the route function to describe the operation.
- Prefer `[OpenApiParameter(...)]` for path/query/header parameters.
- Prefer `[OpenApiRequestBody(...)]` on the *body parameter* (not on the function) when you need request-body metadata.
- Use `[OpenApiResponse(...)]` to describe response status codes and schemas.

### Examples and multi-content types (request/response)

- If an operation accepts multiple request body content types, model them explicitly under `requestBody.content` (e.g. `application/json`, `application/xml`, `application/yaml`, `application/x-www-form-urlencoded`).
- If an operation can respond with multiple formats, model them under `responses['200'|'201'].content` for each media type.
- If you want to reuse examples, prefer component examples + `$ref` (e.g. `#/components/examples/...`) to avoid duplication across multiple media types.

### Response content negotiation (PowerShell routes)

- `Write-KrJsonResponse` always returns JSON (it does not negotiate).
- Use `Write-KrResponse -InputObject ...` when the response should negotiate based on the request `Accept` header.
- Negotiated types include JSON/XML/YAML/form-url-encoded (when supported by the C# response pipeline).

### Schema components (PowerShell class-based)

To register reusable schemas under `components.schemas`, decorate PowerShell classes with:

- `[OpenApiSchemaComponent(...)]`
- `[OpenApiPropertyAttribute(...)]` on properties

If you reference a type in request/response and it is not showing up in `components.schemas`, it usually means the class was not marked as a schema component.

### Callbacks vs webhooks

- **Callbacks** are operation-scoped, live under `paths.{path}.{verb}.callbacks`.
  - Use `[OpenApiCallback]` (inline) or `[OpenApiCallbackRef]` (reference) patterns.
  - Callback URLs are typically provided by the client (e.g., in the request body) and can be expressed with runtime expressions.

- **Webhooks** are top-level OpenAPI 3.1 event notifications, live under `webhooks` (not `components.webhooks`).
  - Use `[OpenApiWebhook]` to declare webhook operations.

### Callback automation (runtime dispatch)

OpenAPI callback attributes describe callbacks in the **OpenAPI document**.
If you want callback functions to actually **dispatch HTTP callback requests at runtime** from PowerShell, enable callback automation middleware:

```powershell
# Enable callback automation middleware (retries/timeouts)
Add-KrAddCallbacksAutomation

# Ensure configuration runs after callback functions are defined
Enable-KrConfiguration
```

**URL template resolution rules (PowerShell callbacks):**

- Callback URL templates can include a request-body runtime expression like `{$request.body#/callbackUrls/status}`.
  - This uses a JSON pointer into the *current request body* (the operation request), so callbacks are typically invoked inside the operation that received those callback URLs.
- Callback URL templates can include `{token}` placeholders (e.g. `{paymentId}`), filled from callback function parameters by name.
- The resolved URL must be absolute, or Kestrun must have a default base URI to combine relative URLs.
- If a required token is missing, dispatch fails with an error indicating the missing token.

**Configuring dispatch:**

- Use `Add-KrAddCallbacksAutomation -DefaultTimeout <sec> -MaxAttempts <n> -BaseDelay <sec> -MaxDelay <sec>` for simple setups.
- Or pass `[Kestrun.Callback.CallbackDispatchOptions]` via `-Options` when you need a typed object.

**Testing guidance:**

- Prefer exercising callback dispatch end-to-end in Pester tutorial tests by posting `callbackUrls.*` that point to a local receiver and asserting dispatch/receiver logs.

### Comment-based help → OpenAPI summary/description

Comment-based help blocks are used by Kestrun to populate OpenAPI `summary`/`description` and parameter descriptions.

- Use standard PowerShell help format: `<# ... #>` (not `<#+ ... #>`).
- Provide `.SYNOPSIS`, `.DESCRIPTION`, and `.PARAMETER` sections on the route function.
- Avoid duplicating the same text in attribute `Summary=`/`Description=` unless you need an override.

### Testing OpenAPI examples (PowerShell)

- Prefer Pester tests under `tests/PowerShell.Tests/`.
- Use the shared harness in `tests/PowerShell.Tests/Kestrun.Tests/PesterHelpers.ps1`:
  - `Start-ExampleScript` to run an example script on a free port
  - `Stop-ExampleScript -Instance ...` to stop it
  - Assert OpenAPI via `Invoke-RestMethod http://localhost:$port/openapi/v3.1/openapi.json`

#### Testing gotchas (content negotiation + examples)

- The tutorial test harness executes scripts from `docs/pwsh/tutorial/examples` (not `docs/_includes/examples`). Keep those copies in sync when changing tutorial behavior.
- When validating negotiated responses with `Invoke-WebRequest`, YAML and `application/x-www-form-urlencoded` bodies may arrive as a byte/int array; decode as UTF-8 before asserting content.
- If you add/modify documentation UI routes via `Add-KrApiDocumentationRoute`, ensure the UI is pointed at the intended OpenAPI endpoint (e.g. `-OpenApiEndpoint '/openapi/v3.1/openapi.json'`) when the example exposes multiple specs.
