<#
    13.2 Server Options Example
    Demonstrates setting server options for denying server header, disabling response header compression, and configuring runspaces.
    File:    13.2-Server-Options.ps1
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
# 1. (Optional) Logging pipeline so we see events
New-KrLogger |
    Set-KrLoggerLevel -Value Debug |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# 2. Create server host
New-KrServer -Name 'Server Limits'

# 3. Add loopback listener on port 5000 (auto unlinks existing file if present)
# This listener will be used to demonstrate server limits configuration.
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# 4. Add PowerShell runtime for script routes and middlewares
Add-KrPowerShellRuntime

# 5. Set server options for denying server header, disabling response header compression, and configuring runspaces
Set-KrServerOptions -DenyServerHeader -MaxRunspaces 8 -MinRunspaces 2

# 6. Finalize configuration and set server limits
Enable-KrConfiguration

# 7. Map a simple info route to demonstrate server options in action
Add-KrMapRoute -Verbs Get -Pattern '/info' -ScriptBlock {
    Write-KrJsonResponse @{ status = 'ok'; time = (Get-Date) }
}

# 8. Start the server (runs asynchronously; press Ctrl+C to stop)
Start-KrServer
