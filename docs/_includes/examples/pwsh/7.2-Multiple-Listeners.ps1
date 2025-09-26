<#
    Sample Kestrun Server - Multiple Listeners
    Demonstrates adding multiple HTTP listeners for the same server instance.
    FileName: 7.2-Multiple-Listeners.ps1
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# (Optional) Configure console logging
New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

$Port2 = $Port + 433
# Create a new Kestrun server
New-KrServer -Name 'Endpoints Multi'

# Loopback listener on primary port
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Second loopback listener on secondary port
Add-KrEndpoint -Port $Port2 -IPAddress $IPAddress

# Add the PowerShell runtime
Add-KrPowerShellRuntime

# Enable Kestrun configuration
Enable-KrConfiguration

# Map a simple ping route
Add-KrMapRoute -Verbs Get -Pattern '/ping' -ScriptBlock {
    Write-KrLog -Level Debug -Message 'Ping requested'
    Write-KrTextResponse -InputObject 'pong' -StatusCode 200
}

Write-KrLog -Level Information -Message 'Multiple listeners configured ({Port}, {Port2})' -Values $Port, $Port2

# Start the server asynchronously
Start-KrServer -CloseLogsOnExit
