---
layout: default
title: Build & Environment
parent: Contributing
nav_order: 1
---
<!-- markdownlint-disable-file MD033 -->
# Build & Environment

This page explains how to set up your environment and build Kestrun from source.

## 1. Prerequisites

| Component | Required | Notes |
|-----------|----------|-------|
| PowerShell | 7.4 / 7.5 / 7.6 (preview) | 7.4/7.5 run on .NET 8; 7.6 preview runs on .NET 10 |
| .NET 8 SDK | Yes (build) | Provides SDK + runtimes (net8.0) |
| .NET 10 SDK | Yes (build) | Provides SDK + runtimes (net10.0) |
| .NET 9 SDK | Optional (build) | Only needed if building net9.0 explicitly |
| ASP.NET Core Runtime 8 | Run-only (optional) | If not building; matches PS 7.4 / 7.5 |
| ASP.NET Core Runtime 9 | Run-only (optional) | If not building; matches PS 7.6 preview |
| InvokeBuild | Yes | Task orchestration (`Install-PSResource`) |
| Pester | Yes | PowerShell tests |

Official download landing pages (always use these for latest secure patches):

| Purpose | .NET 8 | .NET 10 |
|---------|--------|--------|
| SDK | [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) |
| ASP.NET Core Runtime | [.NET 8 ASP.NET Core](https://dotnet.microsoft.com/download/dotnet/8.0) | [.NET 10 ASP.NET Core](https://dotnet.microsoft.com/download/dotnet/10.0) |

PowerShell releases: [GitHub PowerShell Releases](https://github.com/PowerShell/PowerShell/releases)

Install tooling:

```powershell
Install-PSResource -Name 'InvokeBuild','Pester' -Scope CurrentUser
```

Verify SDKs:

```powershell
dotnet --list-sdks
dotnet --list-runtimes | Where-Object { $_ -match 'Microsoft.(AspNetCore|NETCore).App' }
```

Expected (abbreviated):

```text
Microsoft.NETCore.App 8.0.x
Microsoft.AspNetCore.App 8.0.x
Microsoft.NETCore.App 10.0.x
Microsoft.AspNetCore.App 10.0.x
Microsoft.NETCore.App 9.0.x (optional)
Microsoft.AspNetCore.App 9.0.x (optional)
```

## 2. Clone

```powershell
git clone https://github.com/Kestrun/Kestrun.git
cd Kestrun
```

## 3. Restore & Build

```powershell
Invoke-Build Restore
Invoke-Build Build
```

Optional parameters (see `Kestrun.build.ps1`):

| Parameter | Purpose | Example |
|-----------|---------|---------|
| `-Configuration` | Debug/Release | `Invoke-Build Build -Configuration Release` |
| `-Frameworks` | Target TFMs | `Invoke-Build Build -Frameworks net8.0,net10.0` (default)<br/>`Invoke-Build Build -Frameworks net8.0,net9.0,net10.0` (all) |
| `-FileVersion` | Version file path | `Invoke-Build Build -FileVersion ./version.json` |

## 4. Run Examples

```powershell
dotnet run --project .\examples\CSharp\MultiRoutes\MultiRoutes.csproj
```

## 5. PowerShell Module (From Source)

```powershell
Import-Module ./src/PowerShell/Kestrun/Kestrun.psm1 -Force
```

## 6. Build Help & Docs

```powershell
Invoke-Build Build-Help
```

Generated docs land under `docs/` (Just-the-Docs structure).

## 7. Code Formatting

```powershell
Invoke-Build Format
```

## 8. Clean Artifacts

```powershell
Invoke-Build Clean
```

## 9. Coverage

```powershell
Invoke-Build Coverage      # Collect
Invoke-Build Report-Coverage # HTML + badges
```

HTML report lives under `coverage/report`.

## 10. Packaging

```powershell
Invoke-Build Package
```

## 11. Module Install Locally

```powershell
Invoke-Build Install-Module
```

## 12. Common Troubleshooting

| Issue | Fix |
|-------|-----|
| Missing SDK | Re-run installer; check PATH |
| Permission errors | `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` |
| Pester not found | Reinstall: `Install-PSResource Pester` |
| Unicode glyphs garbled | Set a font that supports emoji or suppress icons once helper added |

---
Last updated: {{ site.time | date: '%Y-%m-%d' }}
