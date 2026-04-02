# Kestrun Tool

`Kestrun.Tool` provides the `dotnet-kestrun` .NET tool for running and operating Kestrun PowerShell-hosted services.

## Install

```powershell
dotnet tool install --global Kestrun.Tool
```

If the tool is already installed:

```powershell
dotnet tool update --global Kestrun.Tool
```

## Command name

After installation, run commands with:

```powershell
dotnet kestrun <command> [options]
```

## Core commands

- `run`: run a PowerShell script (`./Service.ps1` by default)
- `module`: manage Kestrun PowerShell module install/update/remove/info
- `service`: install/update/remove/start/stop/query/info service lifecycle
- `info`: display runtime/build diagnostics
- `version`: display tool version

Show command help:

```powershell
dotnet kestrun --help
dotnet kestrun run help
dotnet kestrun module help
dotnet kestrun service help
```

## Quick examples

Run a script:

```powershell
dotnet kestrun run --script .\Service.ps1 --arguments --port 5000
```

Install a service:

```powershell
dotnet kestrun service install --package .\my-kestrun.krpack
```

Install using an explicit offline runtime package:

```powershell
dotnet kestrun service install --package .\my-kestrun.krpack --runtime-package .\Kestrun.Service.win-x64.1.0.0-rc.1.nupkg
```

`--runtime-package` also accepts a folder. In that case Kestrun selects the expected
`Kestrun.Service.<rid>.<version>.nupkg` file for the current platform and target version
from that folder, and returns an error if the file is not present.

Install from a local feed with an explicit runtime cache:

```powershell
dotnet kestrun service install --package .\my-kestrun.krpack --runtime-source .\artifacts\nuget --runtime-cache .\.kestrun-runtime-cache
```

Install from a direct runtime package URL:

```powershell
dotnet kestrun service install --package .\my-kestrun.krpack --runtime-source https://packages.example.com/Kestrun.Service.win-x64.1.0.0-rc.1.nupkg --content-root-bearer-token <token>
```

Install with package checksum verification (default algorithm is SHA-256):

```powershell
dotnet kestrun service install --package .\my-kestrun.krpack --content-root-checksum <hex>
```

Install from a remote package URL:

```powershell
dotnet kestrun service install --package https://downloads.example.com/my-kestrun.krpack
```

Install from an authenticated package URL:

```powershell
dotnet kestrun service install --package https://downloads.example.com/my-kestrun.krpack --content-root-bearer-token <token>
```

Install from a package URL with custom request headers (repeat `--content-root-header` as needed):

```powershell
dotnet kestrun service install --package https://downloads.example.com/my-kestrun.krpack --content-root-header x-api-key:<key> --content-root-header x-env:prod
```

Ignore HTTPS certificate validation for package download (insecure):

```powershell
dotnet kestrun service install --package https://downloads.example.com/my-kestrun.krpack --content-root-ignore-certificate
```

Query service status:

```powershell
dotnet kestrun service query --name my-kestrun
```

Show installed service metadata (including Service.psd1 values and service bundle path):

```powershell
dotnet kestrun service info --name my-kestrun
```

List installed Kestrun services (human-readable default output):

```powershell
dotnet kestrun service info
```

List installed Kestrun services as JSON:

```powershell
dotnet kestrun service info --json
```

Update an installed service bundle from a package and/or module manifest:

```powershell
dotnet kestrun service update --name my-kestrun --package .\my-kestrun.krpack --kestrun-manifest .\src\PowerShell\Kestrun\Kestrun.psd1
```

Update using the repository module only when it is newer than the bundled module:

```powershell
dotnet kestrun service update --name my-kestrun --package .\my-kestrun.krpack --kestrun
```

## Service packaging quick start

Create a Service.psd1 descriptor:

```powershell
New-KrServiceDescriptor `
    -Path .\MyServiceApp\Service.psd1 `
    -Name 'my-service' `
    -Description 'Production Kestrun service' `
    -Version 1.2.0 `
    -EntryPoint '.\Service.ps1' `
    -ServiceLogPath '.\logs\service.log' `
    -PreservePaths @('config/production.json', 'data/', 'logs/')
```

Create a package from a single script (auto-generates Service.psd1):

```powershell
New-KrServicePackage `
    -ScriptPath .\Service.ps1 `
    -Name 'my-service' `
    -Description 'Production Kestrun service' `
    -Version 1.2.0 `
    -OutputPath .\my-service-1.2.0.krpack
```

Fail back to latest backup for application/module:

```powershell
dotnet kestrun service update --name my-kestrun --failback
```

## Notes

- Use `dotnet kestrun module install` when the `Kestrun` PowerShell module is not available.
- `service install` registers the service/daemon but does not auto-start it.
- `service info` without `--name` lists installed Kestrun services.
- `service info` uses human-readable output by default; use `--json` for structured output.
- `service update` requires the service to be stopped.
- `service update --package` only updates the application when package `Version` is greater than installed `Version`.
- `service update` creates backup folders for updated application/module/service-host content.
- `service update --package` respects descriptor `PreservePaths` (relative file/folder paths) and restores those paths from the current
install after package content is applied.
- `service install --package` resolves `Kestrun.Service.<rid>` for the current platform by default.
    Use `--runtime-package` for offline installs or `--runtime-source` to target a
    local/private feed.
- `service install` does not fall back to the runtime bundled with `Kestrun.Tool` when runtime package acquisition fails.
    If the requested runtime package version is unavailable, provide `--runtime-package` or select a different `--runtime-version`.
- runtime packages are cached under an OS-specific cache root unless `--runtime-cache` is supplied.
    Canonical package cache: `packages/<id>/<version>/<id>.<version>.nupkg`.
    Extracted working payload cache: `expanded/<id>/<version>` or `expanded/<id>/<version>-<content-hash>`.
    The tool may reuse valid entries in `expanded/` before attempting a network download.
- default runtime cache roots:
  - Windows: `%ProgramData%\\Kestrun\\RuntimePackages`
  - macOS: `~/Library/Caches/Kestrun/RuntimePackages`
  - Linux: `$XDG_CACHE_HOME/kestrun/runtime-packages` (or `~/.cache/kestrun/runtime-packages`)
- `Service.psd1` example for update-safe content:

```powershell
@{
 FormatVersion = '1.0'
 Name = 'my-kestrun'
 Description = 'Production service package'
 Version = '1.2.0'
 EntryPoint = './Service.ps1'
 PreservePaths = @(
  'config/production.json'
  'data/'
  'logs/'
 )
}
```

- `service update --kestrun` uses `src/PowerShell/Kestrun/Kestrun.psd1` from the current repository and updates bundled module only when repository version is newer;
otherwise it prints a skip message.
- `service update --failback` restores from the latest backup (application/module) and removes that backup folder after restore.
- `service install --package` expects a `.krpack` package.
- runtime package ids default to `Kestrun.Service.<rid>` and versions default to the current `Kestrun.Tool` version.
- `--content-root-bearer-token` sends bearer auth for HTTP(S) package downloads and HTTP(S) runtime-source downloads.
- `--content-root-header <name:value>` adds custom HTTP headers for HTTP(S) package downloads and HTTP(S) runtime-source downloads; it can be repeated.
- `--content-root-ignore-certificate` skips HTTPS certificate validation for package downloads and runtime-source downloads.
- `--content-root-checksum` verifies package integrity before extraction.
- On Windows, global module operations and some service operations may require elevation.

## Repository

- Source: <https://github.com/Kestrun/Kestrun>
- License: MIT
