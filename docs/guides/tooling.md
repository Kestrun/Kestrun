---
layout: default
title: Dotnet Tool
parent: Guides
nav_order: 35
---

# Kestrun Dotnet Tool

Kestrun ships a dotnet tool package named `Kestrun.Tool` that installs the `kestrun` command.
Use it to run scripts and manage service lifecycle from the CLI.

## Requirements

- .NET SDK/runtime 10.0 or newer
- Package id: `Kestrun.Tool`
- Command name: `kestrun`

## Install

### Global install

```powershell
dotnet tool install -g Kestrun.Tool
```

This installs the published NuGet version of `Kestrun.Tool`.

Run from any shell:

```powershell
kestrun help
# or
dotnet kestrun help
```

### Local install (repo scoped)

```powershell
dotnet new tool-manifest --force
dotnet tool install Kestrun.Tool
```

This also resolves from configured NuGet feeds by default (stable/published version).

Run from the repo folder:

```powershell
dotnet tool run kestrun help
```

### Install from local package output

If you built the package locally (for example via
`Invoke-Build Pack-ScriptRunnerTool`), install from `artifacts/tool-packages`
to use a development build instead of the published NuGet build:

```powershell
dotnet tool install Kestrun.Tool --add-source .\artifacts\tool-packages
```

Typical use:

- `dotnet tool install Kestrun.Tool`: installs published NuGet version.
- `dotnet tool install Kestrun.Tool --add-source .\artifacts\tool-packages`: installs local dev package.

## Update and uninstall

```powershell
dotnet tool update -g Kestrun.Tool
dotnet tool uninstall -g Kestrun.Tool
```

For local tools, remove `-g`.

## Command model

Top-level commands:

- `run`
- `service`
- `info`
- `version`

Top-level help:

```powershell
kestrun help
```

Detailed help uses command-first style:

```powershell
kestrun run help
kestrun service help
kestrun info help
kestrun version help
```

## Script path options

`run` and `service install` accept either a positional script path or a named script path:

```powershell
kestrun run .\server.ps1
kestrun run --script .\server.ps1

kestrun service install --name MyService .\server.ps1
kestrun service install --name MyService --script .\server.ps1
```

## Important options

For `run`:

- `--kestrun-manifest <path>`: explicitly use a `Kestrun.psd1` manifest file.
- `--arguments <args...>`: pass remaining values to the script.

For `service install`:

- `--kestrun-manifest <path>`: manifest used by the service runtime.
- `--service-log-path <path>`: service bootstrap and operation log path.
- `--arguments <args...>`: script arguments for installed service execution.

## Service examples

```powershell
kestrun service install --name demo --script .\server.ps1 --service-log-path C:\ProgramData\Kestrun\logs\demo.log
kestrun service start --name demo
kestrun service query --name demo
kestrun service stop --name demo
kestrun service remove --name demo
```

## Build and pack in this repo

```powershell
Invoke-Build Pack-ScriptRunnerTool
```

This writes the tool package to:

- `artifacts/tool-packages/Kestrun.Tool.<version>.nupkg`

---

Return to the [Guides index](./index).
