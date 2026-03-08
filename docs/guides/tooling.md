---
layout: default
title: Dotnet Tool
parent: Guides
nav_order: 35
---

# Kestrun Dotnet Tool

Kestrun ships a dotnet tool package named `Kestrun.Tool`.
It installs the `dotnet-kestrun` tool command, which is typically invoked as `dotnet kestrun`.
Use it to run scripts and manage service lifecycle from the CLI.

## Requirements

- .NET SDK/runtime 10.0 or newer
- Package id: `Kestrun.Tool`
- Command name: `dotnet-kestrun` (invoked as `dotnet kestrun`)

## Install

### Global install

```powershell
dotnet tool install -g Kestrun.Tool
```

This installs the published NuGet version of `Kestrun.Tool`.

Run from any shell:

```powershell
dotnet kestrun help
# optional direct shim form
dotnet-kestrun help
```

### Local install (repo scoped)

```powershell
dotnet new tool-manifest --force
dotnet tool install Kestrun.Tool
dotnet tool restore
```

This also resolves from configured NuGet feeds by default (stable/published version).

Run from the repo folder:

```powershell
dotnet kestrun help
# or
dotnet tool run dotnet-kestrun help
```

`dotnet tool run` resolves the tool manifest from the current directory and its parent chain.
If you run from a folder outside this repo tree (for example `C:\Users\<you>`), it will not find this repo's local manifest.

### Install from local package output

If you built the package locally (for example via
`Invoke-Build Pack-KestrunTool`), install from `artifacts/nuget`
to use a development build instead of the published NuGet build:

```powershell
dotnet tool install -g Kestrun.Tool --add-source .\artifacts\nuget --ignore-failed-sources
```

Typical use:

- `dotnet tool install -g Kestrun.Tool`: installs published NuGet version (works from any folder).
- `dotnet tool install -g Kestrun.Tool --add-source .\artifacts\nuget --ignore-failed-sources`: installs local dev package globally.
- `dotnet tool install --local Kestrun.Tool --add-source .\artifacts\nuget --ignore-failed-sources`: installs local dev package for repo-scoped usage.

If you want `dotnet kestrun` from any folder, install globally.

If you want repo-pinned behavior, install locally and run from inside the manifest tree.

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
dotnet kestrun help
```

Detailed help uses command-first style:

```powershell
dotnet kestrun run help
dotnet kestrun module help
dotnet kestrun service help
dotnet kestrun info help
dotnet kestrun version help
```

## Script path options

`run` and `service install` accept either a positional script path or a named script path:

```powershell
dotnet kestrun run .\server.ps1
dotnet kestrun run --script .\server.ps1

dotnet kestrun service install --name MyService .\server.ps1
dotnet kestrun service install --name MyService --script .\server.ps1
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
- install creates a per-service bundle containing runtime, module, script, and dedicated service-host assets before registration.
- dedicated `kestrun-service-host` is sourced from the `Kestrun.Tool` payload under `kestrun-service/<rid>/`, not from the PowerShell module payload.
- `Modules` are bundled from the PowerShell release matching `Microsoft.PowerShell.SDK` used by ServiceHost and
 copied into the service `Modules` folder during install.
- install shows progress bars for bundle staging and module file copy in interactive terminals.
- bundle roots: Windows `%ProgramData%\Kestrun\services`; Linux `/var/kestrun/services`
  or `/usr/local/kestrun/services` (with user fallback when those are not writable).

## Dedicated service-host direct run

`kestrun-service-host` supports direct script execution with `--run`.

```powershell
kestrun-service-host --run .\server.ps1 --kestrun-manifest .\src\PowerShell\Kestrun\Kestrun.psd1
```

You can still use `--script`, but `--run` is convenient when launching a script directly.

```powershell
kestrun-service-host --script .\server.ps1 --kestrun-manifest .\src\PowerShell\Kestrun\Kestrun.psd1
```

Direct-run defaults:

- `--name` is optional; default is derived from script file name (`kestrun-direct-<scriptName>`).
- `--runner-exe` is optional; default resolves to the current `kestrun-service-host` executable path.
- `--arguments ...` forwards remaining values to the script.
- `--kestrun-manifest` remains required.

## Service examples

```powershell
dotnet kestrun service install --name demo --script .\server.ps1 --service-log-path C:\ProgramData\Kestrun\logs\demo.log
dotnet kestrun service start --name demo
dotnet kestrun service query --name demo
dotnet kestrun service stop --name demo
dotnet kestrun service remove --name demo
```

## Troubleshooting (Windows UAC)

If `dotnet kestrun service install ...` prints `Elevated operation failed` but
`./src/PowerShell/Kestrun/kestrun service install ...` works:

- Run from an elevated shell to bypass UAC relaunch.
- Ensure your selected tool scope is refreshed.

```powershell
# global refresh
dotnet tool uninstall -g Kestrun.Tool
dotnet tool install -g Kestrun.Tool --add-source .\artifacts\nuget --ignore-failed-sources

# local refresh
dotnet tool uninstall --local Kestrun.Tool
dotnet tool install --local Kestrun.Tool --add-source .\artifacts\nuget --ignore-failed-sources
dotnet tool restore
```

## Build and pack in this repo

```powershell
Invoke-Build Pack-KestrunTool
```

This writes the tool package to:

- `artifacts/nuget/Kestrun.Tool.<version>.nupkg`

---

Return to the [Guides index](./index).
