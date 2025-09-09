<#
        Sample: Kestrun Server over a Unix Domain Socket
        Demonstrates binding to a Unix domain socket for local IPC instead of a TCP port.
        File:    7.5-Unix-Sockets.ps1
#>

# 1. (Optional) Logging pipeline so we see events
New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Create server host
New-KrServer -Name 'Endpoints Unix'

# 3. Add Unix socket listener (auto unlinks existing file if present)
Add-KrListenUnixSocket -SocketPath (Join-Path -Path $env:TEMP -ChildPath 'kestrun-demo.sock')

# 4. Add PowerShell runtime for script routes
Add-KrPowerShellRuntime

# 5. Finalize configuration
Enable-KrConfiguration

# 6. Map GET /ux route
Add-KrMapRoute -Verbs Get -Pattern '/ux' -ScriptBlock {
    Write-KrLog -Level Debug -Message 'Unix socket route'
    Write-KrTextResponse -InputObject 'Hello via unix socket'
}

# 7. Informational log
Write-KrLog -Level Information -Message 'Unix socket listener active.'

# 8. Start server (Ctrl+C to stop)
Start-KrServer
