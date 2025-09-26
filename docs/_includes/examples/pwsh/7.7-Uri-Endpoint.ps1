<#
    Sample Kestrun Server - Basic Server
    This script demonstrates the minimal steps to run a Kestrun server
    with a single HTTP listener and one PowerShell route.
    FileName: 7.1-Basic-Server.ps1
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
$uri = [uri]::new("http://$($IPAddress.ToString()):$Port")
# (Optional) Configure console logging so we can see events
New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# Create a new Kestrun server
New-KrServer -Name 'Endpoints Basic'

# Add a listener on the specified port and IP address
Add-KrEndpoint -Uri $uri

# Add the PowerShell runtime
# !!!!Important!!!! this step is required to process PowerShell routes and middlewares
Add-KrPowerShellRuntime

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
