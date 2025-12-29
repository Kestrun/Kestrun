# kestrun-launcher

A command-line tool for running and managing Kestrun applications as standalone processes or Windows services.

## Overview

`kestrun-launcher` is a .NET 10.0 executable that provides a simple interface for:
- Running PowerShell-based Kestrun applications
- Installing Kestrun apps as Windows services
- Managing Windows services (start, stop, uninstall)

## Installation

### As a .NET Global Tool

```bash
dotnet tool install -g kestrun-launcher
```

### Build from Source

```bash
cd src/CSharp/Kestrun.Launcher
dotnet build -c Release
```

The executable will be in `bin/Release/net10.0/kestrun-launcher.exe` (Windows) or `kestrun-launcher` (Linux/macOS).

## Usage

### Running an App

Run a Kestrun app from a PowerShell script:

```bash
kestrun-launcher run /path/to/server.ps1
```

Or from a directory (will look for `server.ps1`, `start.ps1`, `main.ps1`, or `app.ps1`):

```bash
kestrun-launcher run /path/to/app-folder
```

### Installing as a Windows Service

Install a Kestrun app as a Windows service:

```bash
kestrun-launcher install /path/to/app -n MyKestrunService
```

This uses `sc.exe` to create a Windows service that will run the launcher with the specified app.

### Managing Services

Start a service:

```bash
kestrun-launcher start -n MyKestrunService
```

Stop a service:

```bash
kestrun-launcher stop -n MyKestrunService
```

Uninstall a service:

```bash
kestrun-launcher uninstall -n MyKestrunService
```

## Command Reference

### Commands

- `run [path]` - Run a Kestrun app from a folder or script
- `install [path]` - Install a Kestrun app as a Windows service
- `uninstall` - Uninstall a Windows service
- `start` - Start a Windows service
- `stop` - Stop a Windows service

### Options

- `-p, --path <path>` - Path to the Kestrun app folder or script
- `-n, --service-name <name>` - Service name (required for service commands)
- `-h, --help` - Show help message
- `-v, --version` - Show version information

## Examples

### Example 1: Run a Simple Server

Create a file `server.ps1`:

```powershell
$server = New-KrServer -Name 'MyApp'
$server | Add-KrEndpoint -Port 5000
$server | Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Write-KrJsonResponse @{ message = "Hello World!" }
}
$server | Start-KrServer -Wait
```

Run it:

```bash
kestrun-launcher run ./server.ps1
```

### Example 2: Install as a Service

```bash
# Install
kestrun-launcher install ./server.ps1 -n MyKestrunApp

# Start
kestrun-launcher start -n MyKestrunApp

# Check status (using Windows sc.exe)
sc query MyKestrunApp

# Stop
kestrun-launcher stop -n MyKestrunApp

# Uninstall
kestrun-launcher uninstall -n MyKestrunApp
```

## PowerShell Integration

The launcher uses the Microsoft.PowerShell.SDK to run PowerShell scripts in-process. This means:

- Full access to PowerShell cmdlets and modules
- Execution policy is set to `Bypass` by default
- Working directory is set to the script's directory
- Output streams (Error, Warning, Verbose, Debug) are captured and displayed

## Architecture

The launcher consists of:

- **Args.cs** - Command-line argument parser
- **Program.cs** - Main entry point and command routing
- **PowerShellRunner.cs** - In-process PowerShell script execution
- **ServiceController.cs** - Windows service management via sc.exe

## Requirements

- .NET 10.0 Runtime
- Windows (for service management features)
- PowerShell 7.5+ (bundled via Microsoft.PowerShell.SDK)

## Limitations

- Service management features are Windows-only (uses `sc.exe`)
- Linux/macOS support is available for the `run` command only
- Service installation requires Administrator privileges

## Troubleshooting

### Script Not Found

If the launcher reports "No startup script found", ensure your app directory contains one of:
- `server.ps1`
- `start.ps1`
- `main.ps1`
- `app.ps1`

### Service Won't Start

Check Windows Event Viewer for service startup errors. Common issues:
- Script path is incorrect
- Script requires modules that aren't installed
- Permissions issues

### Debug Mode

Set the `DEBUG` environment variable to see stack traces:

```bash
set DEBUG=1
kestrun-launcher run ./server.ps1
```

## See Also

- [Kestrun Documentation](https://kestrun.github.io/Kestrun/)
- [PowerShell SDK](https://github.com/PowerShell/PowerShell)
- [Windows Service Management](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/sc-create)
