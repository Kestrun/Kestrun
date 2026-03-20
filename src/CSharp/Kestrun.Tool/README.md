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

- `run`: run a PowerShell script (`./server.ps1` by default)
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
dotnet kestrun run --script .\server.ps1 --arguments --port 5000
```

Install a service:

```powershell
dotnet kestrun service install --package .\my-kestrun.krpack
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
- `service update --kestrun` uses `src/PowerShell/Kestrun/Kestrun.psd1` from the current repository and updates bundled module only when repository version is newer;
otherwise it prints a skip message.
- `service update --failback` restores from the latest backup (application/module) and removes that backup folder after restore.
- `service install --package` expects a `.krpack` package.
- `--content-root-bearer-token` sends bearer auth for HTTP(S) package downloads.
- `--content-root-header <name:value>` adds custom HTTP headers for HTTP(S) package downloads and can be repeated.
- `--content-root-ignore-certificate` skips HTTPS certificate validation for package downloads.
- `--content-root-checksum` verifies package integrity before extraction.
- On Windows, global module operations and some service operations may require elevation.

## Repository

- Source: <https://github.com/Kestrun/Kestrun>
- License: MIT
