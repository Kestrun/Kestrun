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
- `service`: install/remove/start/stop/query service lifecycle
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
dotnet kestrun service install --name my-kestrun --script .\server.ps1
```

Install a service from a packaged app archive:

```powershell
dotnet kestrun service install --name my-kestrun --content-root .\my-app.zip --script .\server.ps1
```

Install with archive checksum verification (default algorithm is SHA-256):

```powershell
dotnet kestrun service install --name my-kestrun --content-root .\my-app.tgz --content-root-checksum <hex>
```

Install from a remote archive URL:

```powershell
dotnet kestrun service install --name my-kestrun --content-root https://downloads.example.com/my-app.zip --script .\server.ps1
```

Install from an authenticated archive URL:

```powershell
dotnet kestrun service install --name my-kestrun --content-root https://downloads.example.com/my-app.zip --content-root-bearer-token <token> --script .\server.ps1
```

Install from an archive URL with custom request headers (repeat `--content-root-header` as needed):

```powershell
dotnet kestrun service install --name my-kestrun --content-root https://downloads.example.com/my-app.zip --content-root-header x-api-key:<key> --content-root-header x-env:prod --script .\server.ps1
```

Ignore HTTPS certificate validation for archive download (insecure):

```powershell
dotnet kestrun service install --name my-kestrun --content-root https://downloads.example.com/my-app.zip --content-root-ignore-certificate --script .\server.ps1
```

Query service status:

```powershell
dotnet kestrun service query --name my-kestrun
```

## Notes

- Use `dotnet kestrun module install` when the `Kestrun` PowerShell module is not available.
- `service install` registers the service/daemon but does not auto-start it.
- `service install --content-root` accepts folders and archives (`.zip`, `.tar`, `.tgz`, `.tar.gz`).
- `service install --content-root` also accepts HTTP(S) URLs for supported archive formats.
- `--content-root-bearer-token` sends bearer auth for HTTP(S) archive downloads.
- `--content-root-header <name:value>` adds custom HTTP headers for HTTP(S) archive downloads and can be repeated.
- `--content-root-ignore-certificate` skips HTTPS certificate validation for archive downloads.
- `--content-root-checksum` is only used for archive inputs and verifies integrity before extraction.
- On Windows, global module operations and some service operations may require elevation.

## Repository

- Source: <https://github.com/Kestrun/Kestrun>
- License: MIT
