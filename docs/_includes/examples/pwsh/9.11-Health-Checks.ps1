<#
    Sample Kestrun Server Configuration
    This script demonstrates how to expose the health endpoint and register script/http probes.
    FileName: 9.11-Health-Checks.ps1
#>
Import-Module ./src/PowerShell/Kestrun/Kestrun.psm1
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# Create a new Kestrun server
New-KrServer -Name "Health Demo"

# Add a listener on port 5000 and IP address 127.0.0.1 (localhost)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

# Add the PowerShell runtime
Add-KrPowerShellRuntime

# Enable Kestrun configuration
Enable-KrConfiguration

# Add a simple route the HTTP probe can call
Add-KrMapRoute -Verbs Get -Pattern "/ping" -ScriptBlock {
    Write-KrJsonResponse @{ status = 'Healthy'; description = 'Self ping successful' }
}

# Register the health endpoint at /healthz
Add-KrHealthEndpoint -Pattern '/healthz' -DefaultTags 'self' -TreatDegradedAsUnhealthy $true -ProbeTimeout '00:00:05'

# Register a lightweight script-based probe
Add-KrHealthProbe -Name 'Self' -Tags 'self' -ScriptBlock {
    return [Kestrun.Health.ProbeResult]::new([Kestrun.Health.ProbeStatus]::Healthy, 'Main pipeline ready')
}

# Register an HTTP probe that loops back to the local /ping route
Add-KrHealthHttpProbe -Name 'Ping' -Url 'http://127.0.0.1:5000/ping' -Tags 'remote', 'self' -Timeout '00:00:02'

# Start the server asynchronously
Start-KrServer
