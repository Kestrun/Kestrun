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
dotnet tool install -g Kestrun.Tool --add-source .\artifacts\nuget --ignore-failed-sources --prerelease
```

Use `--prerelease` when consuming RC/beta/alpha tool packages.

Typical use:

- `dotnet tool install -g Kestrun.Tool`: installs published NuGet version (works from any folder).
- `dotnet tool install -g Kestrun.Tool --add-source .\artifacts\nuget --ignore-failed-sources --prerelease`: installs local dev prerelease package globally.
- `dotnet tool install --local Kestrun.Tool --add-source .\artifacts\nuget --ignore-failed-sources --prerelease`:
installs local dev prerelease package for repo-scoped usage.

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

# Install from a script folder (copies full folder recursively)
dotnet kestrun service install --name MyService --content-root .\MyServiceApp

# Script is resolved relative to --content-root
dotnet kestrun service install --name MyService --content-root .\MyServiceApp --script .\scripts\start.ps1

# Install from an archive payload (.zip/.tar/.tgz/.tar.gz)
dotnet kestrun service install --name MyService --content-root .\MyServiceApp.zip --script .\scripts\start.ps1

# Install from a remote archive URL
dotnet kestrun service install --name MyService --content-root https://downloads.example.com/MyServiceApp.tgz --script .\scripts\start.ps1

# Install from a remote archive URL with bearer token auth
dotnet kestrun service install --name MyService --content-root https://downloads.example.com/MyServiceApp.tgz --content-root-bearer-token <token> --script .\scripts\start.ps1

# Install from a remote archive URL with custom request headers
dotnet kestrun service install --name MyService --content-root https://downloads.example.com/MyServiceApp.tgz --content-root-header x-api-key:<key> --content-root-header x-env:prod --script .\scripts\start.ps1

# Ignore HTTPS certificate validation for remote archive download (insecure)
dotnet kestrun service install --name MyService --content-root https://downloads.example.com/MyServiceApp.tgz --content-root-ignore-certificate --script .\scripts\start.ps1

# Verify archive checksum before extraction (default algorithm: sha256)
dotnet kestrun service install --name MyService --content-root .\MyServiceApp.tgz --content-root-checksum <hex>

# Explicit checksum algorithm
dotnet kestrun service install --name MyService --content-root .\MyServiceApp.tar.gz --content-root-checksum <hex> --content-root-checksum-algorithm sha512

# Override default per-OS service bundle root
dotnet kestrun service install --name MyService --deployment-root D:\KestrunServices --script .\server.ps1
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
- `--service-user <name>`: install service/daemon to run under a specific OS account.
- `--service-password <secret>`: password for `--service-user` on Windows service accounts.
- `--deployment-root <folder>`: override where per-service bundles are created.
- `--content-root <path>`: copy the full folder or extract a supported archive (`.zip`, `.tar`, `.tgz`, `.tar.gz`) into the service bundle.
- `--content-root` also accepts an HTTP(S) URL that points to one of the supported archive formats.
- `--content-root-checksum <hex>`: verify archive checksum before extraction.
- `--content-root-checksum-algorithm <name>`: checksum algorithm (`md5`, `sha1`, `sha256`, `sha384`, `sha512`). Defaults to `sha256`.
- `--content-root-bearer-token <token>`: sends `Authorization: Bearer <token>` for HTTP(S) archive downloads.
- `--content-root-header <name:value>`: adds custom request headers for HTTP(S) archive downloads. Repeat to send multiple headers.
- `--content-root-ignore-certificate`: skips HTTPS certificate validation for archive downloads (insecure; use only when necessary).
- `--arguments <args...>`: script arguments for installed service execution.
- if `--content-root` is provided and `--script` is also provided, `--script` must be relative to that folder.
- if `--content-root` is provided and `--script` is omitted, default script is `./server.ps1` under that folder.
- if the selected script does not exist inside `--content-root`, install fails with an error.
- if `--content-root` points to an archive, Kestrun extracts it to a temporary folder before bundling.
- if `--content-root-checksum` is provided, `--content-root` must point to a supported archive source: either a local archive file path or an HTTP(S) archive URL
(folder paths are not valid with checksum verification).
- when `--deployment-root` is provided, install writes the service bundle under that root instead of OS defaults.
- install creates a per-service bundle containing runtime, module, script, and dedicated service-host assets before registration.
- dedicated `kestrun-service-host` is sourced from the `Kestrun.Tool` package's internal `kestrun-service` folder under the dotnet tool install location,
 not from the PowerShell module payload.
- `Modules` are bundled from the PowerShell release matching `Microsoft.PowerShell.SDK` used by ServiceHost and
 copied into the service `Modules` folder during install.  This bundling is determined at package build time (during `Build-KestrunTool`),
 not discovered at service install or service runtime.
- install shows progress bars for bundle staging and module file copy in interactive terminals.
- URL content roots are supported for HTTP(S) archive sources.
- `--content-root-header` only applies to HTTP(S) URL content roots.
- `--content-root-ignore-certificate` only applies to HTTPS URL content roots.
- when `--service-user` is provided:
  - Windows: service is registered with that account (password may be required by SCM depending on account type).
    Built-in aliases are supported for convenience: `NetworkService`, `LocalService`, and `LocalSystem`.
    You can also use full names like `NT AUTHORITY\NetworkService`.
  - Linux: installs a systemd system unit (`/etc/systemd/system`) with `User=<name>`; requires root.
  - macOS: installs a LaunchDaemon (`/Library/LaunchDaemons`) with `UserName`; requires root.
- bundle roots: Windows `%ProgramData%\Kestrun\services`; Linux `/var/kestrun/services`
  or `/usr/local/kestrun/services` (with user fallback when those are not writable).
- on Linux, root candidates are used only when writable; otherwise install falls back to `$HOME/.local/share/kestrun/services`.
- default bootstrap/service logs on Linux are written under `$HOME/.local/share/kestrun/logs` unless `--service-log-path` is provided.

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
dotnet kestrun service install --name demo --content-root .\examples\PowerShell\MultiRoutes --script .\MultiRoutes.ps1
dotnet kestrun service install --name demo --deployment-root D:\KestrunServices --script .\server.ps1
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
dotnet tool install -g Kestrun.Tool --add-source .\artifacts\nuget --ignore-failed-sources --prerelease

# local refresh
dotnet tool uninstall --local Kestrun.Tool
dotnet tool install --local Kestrun.Tool --add-source .\artifacts\nuget --ignore-failed-sources --prerelease
dotnet tool restore
```

## Troubleshooting (Linux)

Kestrun service commands on Linux use `systemctl --user` (user-level units), not system-wide units.

- Install and run the tool as the same non-root user.
- Avoid `sudo dotnet tool install -g Kestrun.Tool` unless you intentionally want the tool under root.
- Avoid `sudo dotnet kestrun service ...` unless you intentionally want to manage root's user unit set.

If installed with `sudo` by mistake:

```bash
sudo dotnet tool uninstall -g Kestrun.Tool
dotnet tool install -g Kestrun.Tool
```

Ensure your shell PATH includes your user tools location:

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
```

If `systemctl --user` reports bus/session errors in SSH or headless environments,
enable lingering so your user manager stays available:

```bash
sudo loginctl enable-linger <user>
```

## Build and pack in this repo

```powershell
Invoke-Build Pack-KestrunTool
```

This writes the tool package to:

- `artifacts/nuget/Kestrun.Tool.<version>.nupkg`

---

Return to the [Guides index](./index).
