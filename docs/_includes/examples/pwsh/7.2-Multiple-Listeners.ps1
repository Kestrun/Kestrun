<#
    Sample Kestrun Server - Multiple Listeners
    Demonstrates adding multiple HTTP listeners for the same server instance.
    FileName: 7.2-Multiple-Listeners.ps1
#>

# (Optional) Configure console logging
New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# Create a new Kestrun server
New-KrServer -Name 'Endpoints Multi'

# Loopback listener on port 5000
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Second loopback listener on port 6000
Add-KrListener -Port 6000 -IPAddress ([IPAddress]::Loopback)

# Add the PowerShell runtime
Add-KrPowerShellRuntime

# Enable Kestrun configuration
Enable-KrConfiguration

# Map a simple ping route
Add-KrMapRoute -Verbs Get -Pattern '/ping' -ScriptBlock {
    Write-KrLog -Level Debug -Message 'Ping requested'
    Write-KrTextResponse -InputObject 'pong' -StatusCode 200
}

Write-KrLog -Level Information -Message 'Multiple listeners configured (5000, 6000)'

# Start the server asynchronously
Start-KrServer
