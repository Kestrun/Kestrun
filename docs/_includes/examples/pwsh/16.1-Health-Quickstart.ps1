<#
    Health Quickstart Script
    Migrated from 9.11-Health-Checks.ps1
    Demonstrates basic health endpoint with script + HTTP probes.
#>
## 1. Logging (console sink for visibility)
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

## 2. Server
New-KrServer -Name "Health Demo"

## 3. Listener (loopback port 5000)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

## 4. Runtime (PowerShell execution engine)
Add-KrPowerShellRuntime

## 5. Enable configuration (locks in components)
Enable-KrConfiguration

## 6. Supporting route used by HTTP probe
Add-KrMapRoute -Verbs Get -Pattern "/ping" -ScriptBlock {
    Write-KrJsonResponse @{ status = 'Healthy'; description = 'Self ping successful' }
}

## 7. Health endpoint (treat degraded as unhealthy for tighter SLAs)
Add-KrHealthEndpoint -Pattern '/healthz' -DefaultTags 'self' -TreatDegradedAsUnhealthy $true -ProbeTimeout '00:00:05'

## 8. Script probe (constant healthy)
Add-KrHealthProbe -Name 'Self' -Tags 'self' -ScriptBlock {
    return New-KrProbeResult Healthy 'Main pipeline ready'
}

## 9. HTTP probe (calls /ping)
Add-KrHealthHttpProbe -Name 'Ping' -Url 'http://127.0.0.1:5000/ping' -Tags 'remote', 'self' -Timeout '00:00:02'

## 10. Start server
Start-KrServer
