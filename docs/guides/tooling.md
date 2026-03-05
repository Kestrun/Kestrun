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

`dotnet tool run` resolves the tool manifest from the current directory and its parent chain.
If you run it from a folder outside this repo tree (for example `C:\Users\<you>`), it will not find this repo's manifest.

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

If you want to run `kestrun` from any folder, install globally instead:

```powershell
dotnet tool install -g Kestrun.Tool --add-source <repo-path>\artifacts\tool-packages
```

## Update and uninstall

```powershell
dotnet tool update -g Kestrun.Tool
dotnet tool uninstall -g Kestrun.Tool
```

For local tools, remove `-g`.

## Command model

Top-level commands:

- `run`
- `module`
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
kestrun module help
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

Global options:

- `--nocheck` (alias: `--no-check`): skip the PowerShell Gallery newer-version warning check.

For `module`:

- `module install [--version <version>] [--scope <local|global>]`: install the module into the selected PowerShell module scope.
- `module update [--version <version>] [--scope <local|global>] [--force]`: update the module in the selected scope (latest when version is omitted).
- `module remove [--scope <local|global>]`: remove the module from the selected scope.
- `module info [--scope <local|global>]`: show installed module versions and latest gallery version for the selected scope.
- `module install` and `module update` show progress bars for download, extraction, and file installation when running in an interactive terminal.
- `module remove` shows progress bars for file and folder deletion when running in an interactive terminal.
- `module install` fails when a module is already installed in the selected scope; use `module update` instead.
- `module update` fails when the target version folder already exists unless `--force` is provided.
- `module info` reports semantic versions from manifest metadata (for example, `1.0.0-beta3`) when prerelease data exists.
- Scope defaults to `local`; use `global` to target all-users module path.
- On Windows, `--scope global` for install/update/remove triggers elevation (UAC) when required.
- Installed module folders use the stable numeric module version (for example, `1.0.0` even when installing `1.0.0-beta4`) to match PowerShell module layout expectations.

For `run`:

- `--kestrun-manifest <path>`: explicitly use a `Kestrun.psd1` manifest file.
- `--arguments <args...>`: pass remaining values to the script.

For `service install`:

- `--kestrun-manifest <path>`: manifest used by the service runtime.
- `--service-log-path <path>`: service bootstrap and operation log path.
- `--arguments <args...>`: script arguments for installed service execution.
- install creates a per-service bundle containing runtime, module, and script assets before registration.
- install shows progress bars for bundle staging and module file copy in interactive terminals.
- bundle roots: Windows `%ProgramData%\Kestrun\services`; Linux `/var/kestrun/services`
  or `/usr/local/kestrun/services` (with user fallback when those are not writable).

## Service examples

```powershell
kestrun service install --name demo --script .\server.ps1 --service-log-path C:\ProgramData\Kestrun\logs\demo.log
kestrun service start --name demo
kestrun service query --name demo
kestrun service stop --name demo
kestrun service remove --name demo
```

## Troubleshooting (Windows UAC)

If `dotnet kestrun service install ...` prints `Elevated operation failed` but
`./src/PowerShell/Kestrun/kestrun service install ...` works:

- Run from an elevated shell to bypass UAC relaunch.
- Ensure the local tool package is refreshed when version is unchanged.

```powershell
dotnet tool uninstall Kestrun.Tool --local
Remove-Item "$env:USERPROFILE\.nuget\packages\kestrun.tool\0.0.1" -Recurse -Force
dotnet tool install Kestrun.Tool --local --version 0.0.1 --add-source .\artifacts\tool-packages --no-cache
```

## Build and pack in this repo

```powershell
Invoke-Build Pack-ScriptRunnerTool
```

This writes the tool package to:

- `artifacts/tool-packages/Kestrun.Tool.<version>.nupkg`

---

Return to the [Guides index](./index).
