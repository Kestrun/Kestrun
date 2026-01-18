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

#### Task Argument Binding (PowerShell)
- `New-KrTask -Arguments @{ ... }` injects values into the task runspace as **PowerShell variables** (e.g. `@{ seconds = 10 }` becomes `$seconds`), not as positional args.
- Avoid `param(...)` at the top of `-ScriptBlock` when relying on `-Arguments`, because `param()` binds from `$args` and can shadow the injected variables (leading to tasks that end immediately).
- Preferred pattern:
  - Reference injected variables directly inside the scriptblock (`$seconds`, `$name`, …)
  - Or explicitly read from a variable you set via `-Arguments`.

### Testing Strategy
```powershell
# C# tests via xUnit
Invoke-Build Test-xUnit

# PowerShell tests via Pester
Invoke-Build Test-Pester
```
If the test only involves C# code, you can use `dotnet test` for faster execution.

> **Note (PowerShell tests):** For focused runs of individual Pester tests, prefer `Invoke-Pester` directly (rather than VS Code's test integration), e.g.:
>
> ```powershell
> Invoke-Pester -Path .\tests\PowerShell.Tests\Kestrun.Tests\Tutorial\Tutorial-*.Tests.ps1
> ```

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

### 5. SSE (Server-Sent Events)

Kestrun supports **per-connection SSE** and **server-wide broadcast SSE**.

**Per-connection SSE (a route owns the stream):**

```powershell
Add-KrMapRoute -Verbs Get -Pattern '/sse' {
  Start-KrSseResponse
  Write-KrSseEvent -Event 'connected' -Data '{"ok":true}' -retryMs 2000
  Write-KrSseEvent -Event 'tick' -Data '{"i":1}' -id '1'
}
```

- Use `Start-KrSseResponse` to set `Content-Type: text/event-stream` and disable buffering.
- Use `Write-KrSseEvent` to emit `event:`, `data:`, optional `id:` and `retry:`.

**Broadcast SSE (server keeps connections; you broadcast events):**

```powershell
# Keep a broadcast SSE connection open
Add-KrSseBroadcastMiddleware -Path '/sse/broadcast' -KeepAliveSeconds 15

# From any route, broadcast to all connected clients
Send-KrSseBroadcastEvent -Event 'message' -Data '{"text":"hello"}'
```

- Broadcast SSE uses `Add-KrSseBroadcastMiddleware` + `Send-KrSseBroadcastEvent`.
- Prefer JSON strings for `-Data` (e.g. `ConvertTo-Json -Compress`) to keep SSE payload predictable.

**OpenAPI for SSE:**

- Document SSE responses as `ContentType = 'text/event-stream'` with a `string` schema.
- If the SSE endpoint is mapped internally (not via `Add-KrMapRoute`), ensure the endpoint is also present in the host route registry so it appears in generated OpenAPI.

### 6. SignalR

SignalR is Kestrun's **real-time, bidirectional** option (WebSockets + fallbacks).

```powershell
Add-KrSignalRHubMiddleware -Path '/hubs/kestrun'

# Broadcast messages/events
Send-KrSignalRLog -Level 'Information' -Message 'Hello'
Send-KrSignalREvent -EventName 'PowerShellEvent' -Data @{ ts = (Get-Date) }

# Group targeting
Send-KrSignalRGroupMessage -GroupName 'Admins' -Method 'ReceiveGroupMessage' -Message @{ text = 'Hi Admins' }
```

- Use `Add-KrSignalRHubMiddleware` to expose the hub path.
- Use `Send-KrSignalR*` cmdlets for broadcasting and group targeting.

**OpenAPI for SignalR-adjacent routes:**

- The hub itself is not a classic REST route, but the HTTP endpoints that *trigger* SignalR broadcasts should be documented normally with OpenAPI attributes.

**Testing guidance (PowerShell):**

- Prefer Pester tutorial tests that start scripts via `Start-ExampleScript` (see `tests/PowerShell.Tests/Kestrun.Tests/PesterHelpers.ps1`).
- For SSE routes, keep tests bounded by using small `count` / `intervalMs` values or by testing broadcast APIs instead of holding open infinite streams.
- For SignalR, validate hub-adjacent HTTP routes and (when needed) use the existing SignalR test helpers in the Pester harness.

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

## Complexity & Documentation Rules

### Cyclomatic complexity
- Keep functions/methods at a maximum cyclomatic complexity of **15**.
- If a function exceeds 15, simplify/refactor it (prefer early returns, guard clauses, and small focused helpers).

### Documentation when refactoring
- When you introduce new helper functions/methods as part of a complexity reduction, **each new helper must include XML documentation** (`/// <summary>`, params, returns as applicable).
- Add brief inline comments only where they clarify non-obvious intent (avoid redundant comments).

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
- OpenAPI JSON (3.2): `/openapi/v3.2/openapi.json`
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

### Tags and external docs (OpenAPI 3.2)

- Prefer `Add-KrOpenApiTag` for tag definitions under `tags[]`.
- For OpenAPI 3.2 hierarchical tags, use `-Parent` and `-Kind`.
- Use `-Extensions` to add `x-*` fields to tags (extension keys **must** start with `x-`; invalid keys are skipped with a warning).
- For **document-level** external documentation, use `Add-KrOpenApiExternalDoc` and pass `-Extensions` when needed.
- For **tag-level** external documentation with extensions, create the object via `New-KrOpenApiExternalDoc -Extensions $extensions` and pass it via `Add-KrOpenApiTag -ExternalDocs`.

### Vendor extensions (x-*)

Kestrun supports OpenAPI vendor extensions (`x-*`) in multiple places.

- **Rule**: extension keys must start with `x-` (OpenAPI requirement). Keys without `x-` are ignored (warning logged).
- **Null values**: null extension values are skipped.

**Recommended pattern**: use a single root extension like `x-kestrun-demo` with a small, stable object payload (avoid timestamps/guids when tests assert values).

```powershell
$demoExt = [ordered]@{
  'x-kestrun-demo' = [ordered]@{
    kind = 'trace'
    domain = 'orders'
    stability = 'beta'
    containsPii = $true
    retryable = $false
  }
}
```

**Document-level extensions** (top-level `x-*` fields):

```powershell
$extensions = [ordered]@{
  'x-tagGroups' = @(
    @{ name = 'Common'; tags = @('operations') },
    @{ name = 'Commerce'; tags = @('orders', 'orders.read') }
  )
}
Add-KrOpenApiExtension -Extensions $extensions
```

**Operation-level extensions** (adds `x-*` fields on the OpenAPI operation):

```powershell
function getMuseumHours {
  [OpenApiPath(HttpVerb = 'get', Pattern = '/museum-hours', Tags = 'operations')]
  [OpenApiExtension('x-badges', '{"name":"Beta","position":"before","color":"purple"}')]
  param()
}
```

**Component-level extensions** (adds `x-*` fields on OpenAPI components):

- **Schema components**: use `[OpenApiExtension(...)]` on classes decorated with `[OpenApiSchemaComponent]`.
- **Parameter components**: use `[OpenApiExtension(...)]` on variables decorated with `[OpenApiParameterComponent]` (emits fields under `components.parameters.*.x-*`).
- **Request body components**: use `[OpenApiExtension(...)]` on variables decorated with `[OpenApiRequestBodyComponent]` (emits fields under `components.requestBodies.*.x-*`).
- **Response components**: use `[OpenApiExtension(...)]` on variables decorated with `[OpenApiResponseComponent]` (emits fields under `components.responses.*.x-*`).
- **Header components**: use `New-KrOpenApiHeader -Extensions` (emits fields under `components.headers.*.x-*`).
- **Link components**: use `New-KrOpenApiLink -Extensions` (emits fields under `components.links.*.x-*`).
- **Example components**: use `New-KrOpenApiExample -Extensions` (emits fields under `components.examples.*.x-*`).

```powershell
# Schema component (components.schemas.Address.x-*)
[OpenApiSchemaComponent(RequiredProperties = ('street', 'city'))]
[OpenApiExtension('x-badges', '[{"name":"Beta"},{"name":"PII"}]')]
[OpenApiExtension('x-kestrun-demo', '{"containsPii":true,"owner":"platform"}')]
class Address {
  [string]$street
  [string]$city
}

# Parameter component (components.parameters.correlationId.x-*)
[OpenApiParameterComponent(In = 'Header', Description = 'Correlation id for request tracing')]
[OpenApiExtension('x-kestrun-demo', '{"kind":"trace","format":"uuid"}')]
[string]$correlationId = NoDefault

# Request body component (components.requestBodies.CreateOrderRequestBody.x-*)
[OpenApiRequestBodyComponent(Description = 'Order creation payload', Required = $true, ContentType = 'application/json')]
[OpenApiExtension('x-kestrun-demo', '{"domain":"orders","containsPii":true}')]
[object]$CreateOrderRequestBody = NoDefault

# Response component (components.responses.NotFound.x-*)
[OpenApiResponseComponent(Description = 'Resource not found', ContentType = ('application/json', 'application/xml'))]
[OpenApiExtension('x-kestrun-demo', '{"kind":"error","retryable":false}')]
[object]$NotFound = NoDefault

# Header component (components.headers.X-RateLimit-Reset.x-*)
$headerExt = [ordered]@{
  'x-kestrun-demo' = @{ unit = 'unix-seconds'; source = 'gateway' }
}
$XRateLimitResetHeader = New-KrOpenApiHeader -Schema ([OpenApiInt64]::new()) -Description 'Rate limit reset time' -Extensions $headerExt

# Link component (components.links.GetUserLink.x-*)
$linkExt = [ordered]@{ 'x-kestrun-demo' = @{ kind = 'follow-up'; auth = 'required' } }
$GetUserLink = New-KrOpenApiLink -OperationId 'getUser' -Description 'Fetch the user' -Extensions $linkExt

# Example component (components.examples.GetMuseumHoursResponseExternalExample.x-*)
$exExt = [ordered]@{ 'x-kestrun-demo' = @{ purpose = 'docs'; stability = 'stable' } }
$GetMuseumHoursResponseExternalExample = New-KrOpenApiExample -ExternalValue 'https://example.com/openapi/examples/museum-hours.json' -Extensions $exExt

# DataValue/SerializedValue examples:
# - Use -DataValue/-SerializedValue when you want to preserve the original source representation.
# - In OpenAPI 3.1, these may appear in the JSON as x-oai-dataValue / x-oai-serializedValue.
```

Tutorial references (working examples + matching Pester tests):
- Headers: `docs/_includes/examples/pwsh/10.9-OpenAPI-Component-Header.ps1`
- Links: `docs/_includes/examples/pwsh/10.10-OpenAPI-Component-Link.ps1`
- Examples: `docs/_includes/examples/pwsh/10.13-OpenAPI-Examples.ps1`
- Schemas: `docs/_includes/examples/pwsh/10.2-OpenAPI-Component-Schema.ps1`
- Parameters: `docs/_includes/examples/pwsh/10.4-OpenAPI-Component-Parameter.ps1`
- Responses: `docs/_includes/examples/pwsh/10.5-OpenAPI-Component-Response.ps1`
- Request bodies + responses: `docs/_includes/examples/pwsh/10.6-OpenAPI-Components-RequestBody-Response.ps1`

Example pattern (hierarchy + extensions):

```powershell
$tagExt = [ordered]@{ 'x-displayName' = 'Orders'; 'x-owner' = 'commerce-team' }
$docsExt = [ordered]@{ 'x-docType' = 'reference'; 'x-audience' = 'public' }

$ordersDocs = New-KrOpenApiExternalDoc -Url 'https://example.com/orders' -Description 'Order docs' -Extensions $docsExt

Add-KrOpenApiTag -Name 'operations' -Kind 'category'
Add-KrOpenApiTag -Name 'orders' -Parent 'operations' -Kind 'resource' -ExternalDocs $ordersDocs -Extensions $tagExt
Add-KrOpenApiTag -Name 'orders.read' -Parent 'orders' -Kind 'operation'

function listOrders {
  [OpenApiPath(HttpVerb = 'get', Pattern = '/orders', Tags = ('orders.read', 'orders', 'operations'))]
  param()
}
```

### Modeling operations

- Use `[OpenApiPath(...)]` on the route function to describe the operation.
- Use `[OpenApiResponse(...)]` for simple, inline response documentation (status code + schema).
- Prefer **response components** for reuse across multiple routes (see below).
- Prefer `[OpenApiRequestBody(...)]` on the *body parameter* (not on the function) when you need request-body metadata.

### Response components (reusable responses)

Kestrun supports **reusable response components** under `components.responses`.

**Defining response components (variable-based):**

```powershell
[OpenApiSchemaComponent(RequiredProperties = ('code', 'message'))]
class ErrorResponse {
  [OpenApiPropertyAttribute(Example = 404)]
  [int]$code

  [OpenApiPropertyAttribute(Example = 'Not Found')]
  [string]$message
}

[OpenApiResponseComponent(Description = 'Resource not found', ContentType = ('application/json', 'application/xml'))]
[OpenApiResponseHeaderRef(Key = 'X-Correlation-Id', ReferenceId = 'X-Correlation-Id')]
[OpenApiResponseExampleRef(Key = 'default', ReferenceId = 'NotFoundErrorExample', ContentType = ('application/json', 'application/xml'))]
[ErrorResponse]$NotFound = NoDefault
```

**Referencing response components from routes:**

```powershell
function getThing {
  [OpenApiPath(HttpVerb = 'get', Pattern = '/things/{id}')]
  [OpenApiResponse(StatusCode = '200', Description = 'OK', Schema = [object])]
  [OpenApiResponseRefAttribute(StatusCode = '404', ReferenceId = 'NotFound')]
  param([int]$id)
}
```

**Important rules for response component variables:**

1. **Assignment is REQUIRED**: Every response component variable must have an explicit assignment.
2. Use `= NoDefault` when you do not want an OpenAPI default value.
3. `ReferenceId` must match the response component variable name (e.g., `NotFound`).

#### Parameter components (reusable parameters)

Kestrun supports **reusable parameter components** declared on PowerShell variables and referenced in route functions via `[OpenApiParameterRef]`.

**Defining parameter components:**

```powershell
# Declare a reusable parameter component on a variable
[OpenApiParameterComponent(In = 'Query', Description = 'Page number', Example = 1)]
[ValidateRange(1, 1000)]
[int]$page = 1

[OpenApiParameterComponent(In = 'Header', Description = 'Correlation id for request tracing')]
[string]$correlationId = NoDefault

[OpenApiParameterComponent(In = 'Path', Required = $true, Description = 'Product identifier', Minimum = 1)]
[long]$productId = NoDefault
```

**Critical rules for parameter component variables:**

1. **Assignment is REQUIRED**: Every parameter component variable **must** have an explicit assignment.
2. **Use `NoDefault` when no OpenAPI default should be generated**:
   - Assign `= NoDefault` when the parameter has no default value in the OpenAPI spec.
   - Do **not** use `$null` or omit the assignment.
3. **Assign a concrete default value when appropriate**:
   - `[int]$page = 1` → OpenAPI `schema.default: 1`
   - `[string]$sortBy = 'name'` → OpenAPI `schema.default: 'name'`

**Referencing parameter components in routes:**

```powershell
function listProducts {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/v1/products')]
    param(
        [OpenApiParameterRef(ReferenceId = 'page')]
        [int]$page,

        [OpenApiParameterRef(ReferenceId = 'correlationId')]
        [string]$correlationId,

        [OpenApiParameterRef(ReferenceId = 'productId')]
        [long]$productId
    )
    # ...
}
```

**Using PowerShell validation attributes to shape parameter schemas:**

PowerShell validation attributes automatically map to OpenAPI schema constraints:

- `[ValidateRange(min, max)]` → `schema.minimum`, `schema.maximum`
- `[ValidateSet('a', 'b', 'c')]` → `schema.enum`
- `[ValidatePattern('regex')]` → `schema.pattern`
- `[ValidateLength(min, max)]` → `schema.minLength`, `schema.maxLength`
- `[ValidateCount(min, max)]` → `schema.minItems`, `schema.maxItems`
- `[ValidateNotNullOrEmpty()]` → `schema.minLength: 1`

Example combining attributes:

```powershell
[OpenApiParameterComponent(In = 'Query', Description = 'Sort field')]
[ValidateSet('name', 'price', 'category')]
[string]$sortBy = 'name'
```

Generates OpenAPI:

```yaml
parameters:
  sortBy:
    in: query
    schema:
      type: string
      enum: [name, price, category]
      default: name
```

**Parameter content-type (advanced):**

Parameters can define content rather than a simple schema (useful for complex header payloads):

```powershell
[OpenApiParameterComponent(In = 'Header', ContentType = 'application/json', Description = 'Client context')]
[ClientContext]$clientContext = NoDefault
```

This generates:

```yaml
parameters:
  clientContext:
    in: header
    content:
      application/json:
        schema:
          $ref: '#/components/schemas/ClientContext'
```

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

### OpenAPI scalars (OpenApiUuid / OpenApiDate / ...)

Kestrun includes OpenAPI **scalar wrapper types** (implemented in `OpenApiScalars.cs`) that are treated as primitives by the generator.
They provide a PowerShell-friendly way to model primitives with consistent OpenAPI `type` + `format`.

Common scalar types:

- `OpenApiString`
- `OpenApiUuid` (`string` + `uuid`)
- `OpenApiDate` (`string` + `date`)
- `OpenApiDateTime` (`string` + `date-time`)
- `OpenApiEmail` (`string` + `email`)
- `OpenApiInt32` / `OpenApiInt64`
- `OpenApiNumber` / `OpenApiFloat` / `OpenApiDouble`
- `OpenApiBoolean`

Use these scalars directly as property types, or alias them into **named schema components** so other schemas can `$ref` them.

#### When to use native types vs scalar aliases

- Use native types (`[string]`, `[int]`, `[datetime]`, etc.) when you just need an inline primitive schema.
- Use a scalar alias (a derived class marked with `[OpenApiSchemaComponent]`) when you need a named reusable schema under `components.schemas` and `$ref` references.

#### Pattern: define a reusable scalar alias component

Define a PowerShell class that inherits from an OpenApi scalar wrapper and decorate it with `[OpenApiSchemaComponent]`:

```powershell
[OpenApiSchemaComponent(Example = '2023-10-29')]
class Date : OpenApiDate {}
```

Then use it as a property type anywhere you want `$ref`:

```powershell
[OpenApiSchemaComponent(RequiredProperties = ('ticketDate'))]
class BuyTicketRequest {
    [OpenApiProperty(Description = 'Date that the ticket is valid for.')]
    [Date]$ticketDate
}
```

#### Arrays of reusable primitives

To model an array of a reusable primitive, define another schema component with `Array = $true` and inherit from the primitive component:

```powershell
[OpenApiSchemaComponent(Description = 'List of planned dates', Array = $true)]
class EventDates : Date {}
```

#### Rules / gotchas (important)

- Do **not** expect the base primitive wrappers themselves (like `OpenApiString`) to appear in `components.schemas`.
  Only your **derived** types that are marked with `[OpenApiSchemaComponent]` should become components.
- If you want a `date` (not `date-time`) field represented via `$ref`, use a component like `[Date]` rather than `[datetime]`.
- Put `Format`/`Example`/`Enum` on the **component** (`[OpenApiSchemaComponent(...)]`) so you don't have to repeat metadata on every property.
- If a derived primitive component is missing from `components.schemas`, first verify it has `[OpenApiSchemaComponent]`.

### Request body components (reusable requestBodies)

Kestrun supports **request body components** under `components.requestBodies`.

- Declare them on **PowerShell variables/properties**, not on classes.
- The component name defaults to the **variable name**; override with `Key = '...'` on `[OpenApiRequestBodyComponent]`.
- If you don't want an OpenAPI `schema.default` emitted for the body schema, assign `= NoDefault` (recommended for most request bodies).

Example:

```powershell
[OpenApiSchemaComponent(RequiredProperties = ('name', 'price'))]
class CreateProductRequest {
  [string]$name
  [double]$price
}

[OpenApiRequestBodyComponent(
  Description = 'Product creation payload',
  Required = $true,
  ContentType = ('application/json', 'application/xml')
)]
[CreateProductRequest]$CreateProductRequest = NoDefault
```

### Request bodies: prefer typed, fall back to OpenApiRequestBodyRef

Kestrun prefers request-body components automatically when the parameter type matches a `components.requestBodies` entry.
However, in PowerShell examples/tests, **script-defined types may not be visible in per-request runspaces**, so the runtime parameter should sometimes be `[object]`.

Preferred patterns:

```powershell
# Typed (preferred when the type is resolvable at runtime)
function createThing {
  [OpenApiPath(HttpVerb = 'post', Pattern = '/things')]
  param(
    [OpenApiRequestBody(ContentType = 'application/json')]
    [CreateThingRequest]$Body
  )
}

# Explicit reference (use when parameter is [object])
function createThing {
  [OpenApiPath(HttpVerb = 'post', Pattern = '/things')]
  param(
    [OpenApiRequestBodyRef(ReferenceId = 'CreateThingRequest', Required = $true)]
    [object]$Body
  )
}
```

### Callbacks vs webhooks

- **Callbacks** are operation-scoped, live under `paths.{path}.{verb}.callbacks`.
  - Use `[OpenApiCallback]` (inline) or `[OpenApiCallbackRef]` (reference) patterns.
  - Callback URLs are typically provided by the client (e.g., in the request body) and can be expressed with runtime expressions.

- **Webhooks** are top-level OpenAPI 3.1 event notifications, live under `webhooks` (not `components.webhooks`).
  - Use `[OpenApiWebhook]` to declare webhook operations.

### HTTP QUERY Method (OpenAPI 3.2+)

The HTTP `QUERY` method is a semantically clearer alternative to `GET` for search/filter operations that require a request body.
It combines safe semantics (like GET) with structured payloads (like POST).

**When to use QUERY:**

- Complex search filters that don't fit cleanly in query parameters.
- Want GET-like semantics (safe, idempotent, repeatable).
- Need to send structured data (filters, facets, advanced criteria) in request body.
- Example: Product search with multiple filter dimensions, date ranges, category selections.

**Implementation:**

Use `HttpVerb = 'query'` in the `[OpenApiPath]` attribute:

```powershell
function searchProducts {
    [OpenApiPath(HttpVerb = 'query', Pattern = '/v1/products/search')]
    [OpenApiResponse(StatusCode = '200', Description = 'Paginated results')]
    param(
        [OpenApiParameterRef(ReferenceId = 'page')]
        [int]$page,

        [OpenApiParameterRef(ReferenceId = 'pageSize')]
        [int]$pageSize,

        [OpenApiRequestBody(Description = 'Search filters')]
        [ProductSearchRequest]$filters
    )

    # Filter logic and return results
    Write-KrResponse -InputObject $result -StatusCode 200
}
```

**OpenAPI Generation:**

- **OpenAPI 3.2+**: Operations appear under `paths['/pattern'].query` (native `query` HTTP verb support).
- **OpenAPI 3.0 / 3.1**: Operations appear under `paths['/pattern'].x-oai-additionalOperations.QUERY` (fallback extension).
  - This allows consumers to understand and invoke QUERY operations even in systems that only support OpenAPI 3.0/3.1.

**Client Usage:**

```powershell
# PowerShell 7.4+
$body = @{
    q = 'laptop'
    minPrice = 500
    maxPrice = 2000
} | ConvertTo-Json

Invoke-WebRequest -Uri 'http://api.example.com/v1/products/search?page=1&pageSize=10' `
    -CustomMethod 'QUERY' `
    -ContentType 'application/json' `
    -Body $body
```

```javascript
// JavaScript
const response = await fetch('http://api.example.com/v1/products/search?page=1&pageSize=10', {
    method: 'QUERY',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ q: 'laptop', minPrice: 500, maxPrice: 2000 })
});
```

**Decision Matrix (QUERY vs GET vs POST):**

| Method | Request Body | Semantics | Use Case |
| :--- | :--- | :--- | :--- |
| **GET** | ❌ No | Safe, idempotent, cacheable | Simple retrieval, no complex filters |
| **POST** | ✅ Yes | Unsafe, creates resource | Create, modify, complex transformations |
| **QUERY** (3.2+) | ✅ Yes | Safe, idempotent (like GET) | **Search/filter with complex criteria** |

See the complete tutorial example at [docs/pwsh/tutorial/10.19-Product-Search-Query.md](/docs/pwsh/tutorial/10.19-Product-Search-Query.md) and the guide section in [docs/guides/openapi.md#941-http-query-method-openapi-32](/docs/guides/openapi.md#941-http-query-method-openapi-32).


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

---

## Tutorial Authoring Guidelines

When generating or modifying tutorial documentation pages under `docs/pwsh/tutorial/*`, follow these strict directives:

### Core Requirements

1. **File Naming**: Use pattern `N.Title.md` (e.g., `6.Redirects.md`) in the appropriate section folder.
2. **Front Matter**: Include YAML fields: `title`, `parent`, `nav_order`, `layout: default`.
3. **H1 Heading**: Must exactly match the `title` value in front matter.
4. **Introduction**: One sentence describing the tutorial's purpose immediately after the H1.
5. **Required Sections** (in order):
   - `## Full source` — References and includes the example script
   - `## Step-by-step` — Numbered list (1–8 steps) describing script actions
   - `## Try it` — Executable examples (curl + PowerShell alternatives)
   - Feature-specific explanatory sections (optional, e.g., `## Redirect Types`)
   - `## References` — Links to all cmdlets and related guides
   - `---` followed by `### Previous / Next` navigation block with `{: .fs-4 .fw-500}`

6. **Include Directive**: In `## Full source`, use `{% include examples/pwsh/FILENAME.ps1 %}` to embed the example.
7. **Script References**: Define link references at the bottom for the example script path.
8. **Navigation**: Append `### Previous / Next` with links to adjacent tutorials (or `_None_` if at section boundary).

### Formatting Standards

- **Code Fences**: Always include language identifiers: `powershell`, `json`, `yaml`, `markdown`.
- **Step List**: Capitalize first word or use format `ComponentName: description`.
- **Line Length**: Keep lines under ~160 characters.
- **Inline Formatting**: Use backticks for cmdlets, routes, and file names.
- **Avoid HTML**: Use plain Markdown only.

### Health-Specific Notes

When authoring Health probe tutorials:

- Use `New-KrProbeResult` instead of direct constructors.
- Ensure `## References` includes endpoint cmdlets, probe cmdlets, and `[Health Guide](/guides/health)`.
- Wrap JSON output in fenced `json` blocks.
- Mention tag filtering, degraded status, or timeouts only if present in the code.

### Redirect / Response Pattern Notes

- Redirect examples must call `Write-KrRedirectResponse`.
- Always show at least one route mapping and complete server startup sequence.

### Verification Checklist

Before completing a tutorial, verify:

- [ ] Filename matches `N.Title.md` pattern
- [ ] Front matter valid and complete
- [ ] H1 matches title exactly
- [ ] Intro sentence present
- [ ] `## Full source` includes correct include path
- [ ] Script reference link definition at bottom
- [ ] `## Step-by-step` numbered (1–n)
- [ ] `## Try it` with executable examples
- [ ] All used cmdlets in `## References`
- [ ] Health page includes Health guide link (if applicable)
- [ ] Navigation block has valid adjacent links (or `_None_`)
- [ ] No placeholder tokens remain (TITLE, SECTION_PARENT, NUMBER, etc.)
- [ ] All code fences have language identifiers
- [ ] Passes `.\Utility\Test-TutorialDocs.ps1 -Path "docs/pwsh/tutorial/N.Title.md"`

### Standard Template

```markdown
---
title: TITLE
parent: SECTION_PARENT
nav_order: NUMBER
layout: default
---

# TITLE

One sentence purpose statement.

## Full source

File: [`pwsh/tutorial/examples/SECTION.NUMBER-slug.ps1`][SECTION.NUMBER-slug.ps1]

\`\`\`powershell
{% include examples/pwsh/SECTION.NUMBER-slug.ps1 %}
\`\`\`

## Step-by-step

1. Component: Brief description of action.
2. Component: Brief description of action.

## Try it

\`\`\`powershell
curl -i http://127.0.0.1:5000/route
\`\`\`

## Key Points

- Point 1
- Point 2

## References

- [Cmdlet-Name][Cmdlet-Name]
- [Guide-Name][Guide-Link]

---

### Previous / Next

{: .fs-4 .fw-500}

Previous: [Previous-Title](./prev-file)
Next: [Next-Title](./next-file)

[SECTION.NUMBER-slug.ps1]: /pwsh/tutorial/examples/SECTION.NUMBER-slug.ps1
[Cmdlet-Name]: /pwsh/cmdlets/Cmdlet-Name
[Guide-Link]: /guides/guide-name
```

### Important Notes

- Maintain ordering: Title → Intro → Full source → Step-by-step → Try it → Feature blocks → (Key Points / Troubleshooting optional) → References → Navigation → Link refs.
- Do not omit `## Full source`, `## Step-by-step`, `## References`, or navigation unless explicitly instructed.
- Use imperative, consistent style; avoid redundant commentary.
- If conventions change, update this section and `docs/contributing/tutorial-template.md`.
