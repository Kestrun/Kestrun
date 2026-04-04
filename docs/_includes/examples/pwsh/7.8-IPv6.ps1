<#
    Sample Kestrun Server - Basic Server
    This script demonstrates the minimal steps to run a Kestrun server
    with a single HTTP listener using IPv6 and one PowerShell route.
    FileName: 7.1-Basic-Server.ps1
#>
param(
    [int]$Port = $env:PORT ?? 5000
)
# (Optional) Configure console logging so we can see events
New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# Create a new Kestrun server
New-KrServer -Name 'Endpoints Basic'

# Add an IPv6 loopback listener
Add-KrEndpoint -Port $Port -AddressFamily InterNetworkV6

# Enable Kestrun configuration
Enable-KrConfiguration

# Map the route
Add-KrMapRoute -Verbs Get -Pattern '/hello' -ScriptBlock {
    Write-KrLog -Level Information -Message 'Processing /hello request'
    Write-KrTextResponse -InputObject 'Hello from basic server' -StatusCode 200
}

# Initial informational log
Write-KrLog -Level Information -Message 'Server {Name} configured.' -Values 'Endpoints Basic'

# Start the server asynchronously
Start-KrServer -CloseLogsOnExit
