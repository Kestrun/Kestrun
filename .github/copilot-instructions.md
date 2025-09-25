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

## Project-Specific Conventions

### 1. PowerShell Cmdlet Patterns
All cmdlets use `Kr` prefix with fluent chaining:
```powershell
New-KrServer -Name 'MyServer' |
    Add-KrListener -Port 5000 |
    Add-KrPowerShellRuntime |
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
    Set-KrLoggerMinimumLevel -Value Debug |
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
