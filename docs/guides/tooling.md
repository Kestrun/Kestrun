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

`run` accepts either a positional script path or a named script path:

```powershell
dotnet kestrun run .\Service.ps1
dotnet kestrun run --script .\Service.ps1

# Install from a service package (.krpack)
dotnet kestrun service install --package .\my-service.krpack

# Install with an explicit local runtime package (offline)
dotnet kestrun service install --package .\my-service.krpack --runtime-package .\Kestrun.Service.win-x64.1.0.0-rc.1.nupkg

# Install with a runtime-package folder; Kestrun selects the expected RID/version package
dotnet kestrun service install --package .\my-service.krpack --runtime-package .\artifacts\nuget --runtime-version 1.0.0-rc.1

# Install using a local/private runtime feed and explicit cache root
dotnet kestrun service install --package .\my-service.krpack --runtime-source .\artifacts\nuget --runtime-cache .\.kestrun-runtime-cache

# Prefetch only the runtime package into cache without installing a service bundle
dotnet kestrun service install --runtime-version 1.0.0-rc.1 --runtime-source .\artifacts\nuget --runtime-cache .\.kestrun-runtime-cache

# Install from a direct runtime package URL
dotnet kestrun service install --package .\my-service.krpack --runtime-source https://packages.example.com/Kestrun.Service.win-x64.1.0.0-rc.1.nupkg --content-root-bearer-token <token>

# Install from a remote package URL
dotnet kestrun service install --package https://downloads.example.com/my-service.krpack

# Install from a remote package URL with custom request headers
dotnet kestrun service install --package https://downloads.example.com/my-service.krpack --content-root-header x-api-key:<key> --content-root-header x-env:prod

# Ignore HTTPS certificate validation for a remote package URL (insecure)
dotnet kestrun service install --package https://downloads.example.com/my-service.krpack --content-root-ignore-certificate

# Verify package checksum before extraction (default algorithm: sha256)
dotnet kestrun service install --package .\my-service.krpack --content-root-checksum <hex>

# Update an installed service from a package
dotnet kestrun service update --name MyService --package .\my-service-v2.krpack

# Override default per-OS service bundle root
dotnet kestrun service install --package .\my-service.krpack --deployment-root D:\KestrunServices
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

For `service start`, `service stop`, and `service query`:

- Default output is a cross-platform normalized table.
- `--json`: return normalized output as JSON.
- `--raw`: return native OS command output (Windows `sc.exe`, Linux `systemctl`, macOS `launchctl`).

Example output modes (`service query`):

```powershell
# Default: normalized table output
dotnet kestrun service query --name demo

# JSON: normalized structured output
dotnet kestrun service query --name demo --json

# Raw: native OS command output
dotnet kestrun service query --name demo --raw
```

Typical default table output:

```text
Operation | Service | Platform | Status  | State   | PID  | ExitCode | Message
----------+---------+----------+---------+---------+------+----------+----------------
query     | demo    | windows  | success | running | 8420 | 0        | STATE : RUNNING
```

Typical `--json` output:

```json
{
  "Operation": "query",
  "ServiceName": "demo",
  "Platform": "windows",
  "Status": "success",
  "State": "running",
  "PID": 8420,
  "ExitCode": 0,
  "Message": "STATE : RUNNING"
}
```

Values in `Platform`, `State`, and `Message` vary by OS and actual service state, but the output schema stays consistent.

For `service install`:

- `--package <path-or-url>`: `.krpack` package containing `Service.psd1` and application files.
- `--kestrun-manifest <path>`: manifest used by the service runtime.
- `--service-log-path <path>`: service bootstrap and operation log path.
- `--service-user <name>`: install service/daemon to run under a specific OS account.
- `--service-password <secret>`: password for `--service-user` on Windows service accounts.
- `--deployment-root <folder>`: override where per-service bundles are created.
- `--runtime-source <path-or-url>`: resolve runtime packages from a local folder, a direct `.nupkg`, a direct `.nupkg` URL, or a NuGet feed endpoint.
- `--runtime-package <path-to-.nupkg-or-folder>`: use an explicit local runtime package file or a folder containing `Kestrun.Service.<rid>.<version>.nupkg` artifacts.
- `--runtime-version <version>`: override the runtime package version; when used without `--package`, install only populates the runtime cache.
- `--runtime-package-id <id>`: override the default runtime package id (`Kestrun.Service.<rid>`).
- `--runtime-cache <folder>`: override the local runtime package cache root.
- `Service.psd1` required keys inside the package: `FormatVersion` (must be `'1.0'`), `Name`, `Description`, `Version`, `EntryPoint`.
- `Service.psd1` optional keys: `ServiceLogPath`, `PreservePaths`, `ApplicationDataFolders`.
- `Version` in `Service.psd1` must be compatible with `System.Version` parsing.
- If both `Service.psd1` and CLI provide a service log path, `--service-log-path` overrides descriptor `ServiceLogPath`.
- Package installs create bundles at `<deployment-root>/<Name>/`.
- Runtime-only prefetch (`service install` without `--package`) requires at least one runtime acquisition option and does not create a service bundle.
- `PreservePaths` is an optional string array of relative file/folder paths to keep from the currently installed application during `service update --package`.
- `PreservePaths` entries must be relative and resolve inside the service application root (absolute paths and root-escaping paths are rejected).
- `ApplicationDataFolders` is an optional string array of relative application-data folders to keep from the currently installed application during `service update --package`.
- `ApplicationDataFolders` entries must be relative and resolve inside the service application root (absolute paths and root-escaping paths are rejected).

Example `Service.psd1` with `PreservePaths`:

```powershell
@{
  FormatVersion = '1.0'
  Name = 'MuseumService'
  Description = 'Museum API service bundle'
  Version = '1.2.0'
  EntryPoint = './Service.ps1'
  PreservePaths = @(
    'config/settings.json'
    'db/app.db'
  )
  ApplicationDataFolders = @(
    'data/'
    'logs/'
  )
}
```

- Files and folders listed in `PreservePaths` and `ApplicationDataFolders` are copied from the currently installed application before package replacement and restored afterward.
- `--content-root-checksum <hex>`: verify package checksum before extraction.
- `--content-root-checksum-algorithm <name>`: checksum algorithm (`md5`, `sha1`, `sha256`, `sha384`, `sha512`). Defaults to `sha256`.
- `--content-root-bearer-token <token>`: sends `Authorization: Bearer <token>` for HTTP(S) package downloads and HTTP(S) runtime-source downloads.
- `--content-root-header <name:value>`: adds custom request headers for HTTP(S)
  package downloads and HTTP(S) runtime-source downloads. Repeat to send
  multiple headers.
- `--content-root-ignore-certificate`: skips HTTPS certificate validation for
  HTTPS package downloads and HTTPS runtime-source downloads (insecure; use
  only when necessary).
- `--arguments <args...>`: script arguments for installed service execution.
- `service install --package` resolves `Kestrun.Service.<rid>` for the current platform automatically.
- `--runtime-package` folder input selects the expected
  `Kestrun.Service.<rid>.<version>.nupkg` file for the current platform and
  target version and fails if that file is missing.
- Runtime cache layout uses a canonical package cache at
  `packages/<id>/<version>/<id>.<version>.nupkg` and extracted working payloads
  under `expanded/<id>/<version>` or
  `expanded/<id>/<version>-<content-hash>`.
- When `--deployment-root` is provided, install writes the service bundle under that root instead of OS defaults.
- Install creates a per-service bundle containing runtime, module, script, and dedicated service-host assets before registration.
- Install does not fall back to the runtime bundled with `Kestrun.Tool` when runtime package acquisition fails.
- `Modules` are bundled from the PowerShell release matching `Microsoft.PowerShell.SDK` used by ServiceHost and
 copied into the service `Modules` folder during install.  This bundling is determined at package build time (during `Build-KestrunTool`),
 not discovered at service install or service runtime.
- Install shows progress bars for bundle staging and module file copy in interactive terminals.
- When `--service-user` is provided:
  - Windows: service is registered with that account (password may be required by SCM depending on account type).
    Built-in aliases are supported for convenience: `NetworkService`, `LocalService`, and `LocalSystem`.
    You can also use full names like `NT AUTHORITY\NetworkService`.
  - Linux: installs a systemd system unit (`/etc/systemd/system`) with `User=<name>`; requires root.
  - macOS: installs a LaunchDaemon (`/Library/LaunchDaemons`) with `UserName`; requires root.
- Bundle roots: Windows `%ProgramData%\Kestrun\Services`; Linux `/var/kestrun/services`
  or `/usr/local/kestrun/services` (with user fallback when those are not writable).
- On Linux, root candidates are used only when writable; otherwise install falls back to `$HOME/.local/share/kestrun/services`.
- Default bootstrap and service logs on Linux are written under `$HOME/.local/share/kestrun/logs` unless `--service-log-path` is provided.

## .krpack lifecycle (create, check, modify)

Use this flow when you want a repeatable package artifact for install/update.

### 1. Create a package

```powershell
# Create/update Service.psd1 in your app root
$newDescriptorParams = @{
  Path = '.\MyServiceApp'
  Name = 'MuseumService'
  Description = 'Museum API service bundle'
  Version = [Version]'1.2.0'
  EntryPoint = './Service.ps1'
  PreservePaths = @('config/settings.json', 'data/', 'logs/')
}
New-KrServiceDescriptor @newDescriptorParams

# Build .krpack from the folder
New-KrServicePackage -SourceFolder .\MyServiceApp -OutputPath .\museum-service-1.2.0.krpack
```

### 2. Check a package

```powershell
# Inspect descriptor contents from an existing package
$tmp = Join-Path $env:TEMP ("kestrun-krpack-inspect-" + [guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath .\museum-service-1.2.0.krpack -DestinationPath $tmp -Force
Get-KrServiceDescriptor -Path $tmp

# Optional integrity hash for distribution/sign-off
Get-FileHash .\museum-service-1.2.0.krpack -Algorithm SHA256
```

### 3. Modify and repack

```powershell
# Expand package, update descriptor metadata, then repack
$tmp = Join-Path $env:TEMP ("kestrun-krpack-edit-" + [guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath .\museum-service-1.2.0.krpack -DestinationPath $tmp -Force

$setDescriptorParams = @{
  Path = $tmp
  Version = [Version]'1.2.1'
  Description = 'Museum API service bundle v1.2.1'
  EntryPoint = './Service.ps1'
  PreservePaths = @('config/settings.json', 'data/', 'logs/')
}
Set-KrServiceDescriptor @setDescriptorParams

New-KrServicePackage -SourceFolder $tmp -OutputPath .\museum-service-1.2.1.krpack -Force
```

`Service.psd1` in `.krpack` is format `1.0`; `FormatVersion` and `EntryPoint` are required.
`-Path` for descriptor cmdlets may be the descriptor file path or the containing directory.
`EntryPoint` must be a relative path that resolves to an existing file inside the descriptor directory.

## Dedicated service-host direct run

`kestrun-service-host` supports direct script execution with `--run`.

```powershell
kestrun-service-host --run .\Service.ps1 --kestrun-manifest .\src\PowerShell\Kestrun\Kestrun.psd1
```

You can still use `--script`, but `--run` is convenient when launching a script directly.

```powershell
kestrun-service-host --script .\Service.ps1 --kestrun-manifest .\src\PowerShell\Kestrun\Kestrun.psd1
```

Direct-run defaults:

- `--name` is optional; default is derived from script file name (`kestrun-direct-<scriptName>`).
- `--runner-exe` is optional; default resolves to the current `kestrun-service-host` executable path.
- `--arguments ...` forwards remaining values to the script.
- `--kestrun-manifest` remains required.

## Service examples

```powershell
dotnet kestrun service install --package .\demo.krpack
dotnet kestrun service install --package .\demo.krpack --deployment-root D:\KestrunServices
dotnet kestrun service start --name demo
dotnet kestrun service query --name demo
dotnet kestrun service stop --name demo
dotnet kestrun service update --name demo --package .\demo-v2.krpack
dotnet kestrun service update --name demo --failback
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

## Troubleshooting (Windows service remove cleanup)

`dotnet kestrun service remove --name <service>` now waits for service stop and retries bundle cleanup before warning.

If you still see a warning like `Failed to remove service bundle ... file is being used by another process`:

- Wait a few seconds for process teardown to complete.
- Re-run `dotnet kestrun service remove --name <service>` to finish bundle cleanup.
- If needed, stop any process still using files under `%ProgramData%\Kestrun\Services\<service>` and remove the folder.

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
- `artifacts/nuget/Kestrun.Service.<rid>.<version>.nupkg`

`service install --package` resolves `Kestrun.Service.<rid>` for the current platform automatically.
Use `--runtime-package` for offline installs or `--runtime-source` to target a local feed,
direct package artifact, or NuGet endpoint.

`service install` requires a resolvable runtime package and does not fall back to the runtime
payload bundled with `Kestrun.Tool` when package acquisition fails.

Runtime cache layout defaults to:

- canonical package cache: `packages/<id>/<version>/<id>.<version>.nupkg`
- extracted working payload cache: `expanded/<id>/<version>` or `expanded/<id>/<version>-<content-hash>`

---

Return to the [Guides index](./index).
