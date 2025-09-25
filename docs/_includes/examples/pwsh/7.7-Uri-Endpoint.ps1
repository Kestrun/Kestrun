<#
    Sample Kestrun Server - Basic Server
    This script demonstrates the minimal steps to run a Kestrun server
    with a single HTTP listener and one PowerShell route.
    FileName: 7.1-Basic-Server.ps1
#>

# (Optional) Configure console logging so we can see events
New-KrLogger |
    Add-KrSinkConsole |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# Create a new Kestrun server
New-KrServer -Name 'Endpoints Basic'

# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrEndpoint -Uri ([uri]::new('http://localhost:5000'))

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
Write-KrLog -Level Information -Message 'Server {Name} configured.' -Properties 'Endpoints Basic'

# Start the server asynchronously
Start-KrServer -CloseLogsOnExit
