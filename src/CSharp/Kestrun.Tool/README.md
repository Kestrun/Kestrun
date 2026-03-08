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

Query service status:

```powershell
dotnet kestrun service query --name my-kestrun
```

## Notes

- Use `dotnet kestrun module install` when the `Kestrun` PowerShell module is not available.
- `service install` registers the service/daemon but does not auto-start it.
- On Windows, global module operations and some service operations may require elevation.

## Repository

- Source: <https://github.com/Kestrun/Kestrun>
- License: MIT
