<#
    13.1 Server Limits Example
    Demonstrates setting server limits for request body size, concurrent connections, and other limits.
    File:    13.1-Server-Limits.ps1
#>

# 1. (Optional) Logging pipeline so we see events
New-KrLogger |
    Set-KrLoggerMinimumLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Create server host
New-KrServer -Name 'Server Limits'

# 3. Add loopback listener on port 5000 (auto unlinks existing file if present)
# This listener will be used to demonstrate server limits configuration.
Add-KrEndpoint -Port 5000 -IPAddress ([IPAddress]::Loopback)

# 4. Add PowerShell runtime for script routes and middlewares
Add-KrPowerShellRuntime

# 5. Set server limits for request body size, concurrent connections, and other limits
Set-KrServerLimit -MaxRequestBodySize 1048576 -MaxConcurrentConnections 1 -KeepAliveTimeoutSeconds 60 -MaxRequestHeadersTotalSize 16384

# 6. Finalize configuration and set server limits
Enable-KrConfiguration

# 7. Map a simple info route to demonstrate server limits in action
Add-KrMapRoute -Verbs Get -Pattern '/info' -ScriptBlock {
    Start-Sleep 20
    Write-KrJsonResponse @{ status = 'ok'; time = (Get-Date) }
}

# 8. Start the server (runs asynchronously; press Ctrl+C to stop)
Start-KrServer
