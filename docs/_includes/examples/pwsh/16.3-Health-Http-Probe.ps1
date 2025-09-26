<#
 HTTP Probe Example
 Demonstrates adding an HTTP health probe calling a local route.
#>
## 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

## 2. Server
New-KrServer -Name 'Health HTTP Probe'

## 3. Listener (port 5002)
Add-KrListener -Port 5002 -IPAddress ([IPAddress]::Loopback)

## 4. Runtime
Add-KrPowerShellRuntime

## 5. Enable configuration
Enable-KrConfiguration

## 6. Supporting /status route simulating downstream component
Add-KrMapRoute -Verbs Get -Pattern '/status' -ScriptBlock {
    Write-KrJsonResponse @{ status = 'Healthy'; description = 'Component OK'; version = '1.0.3' }
}

## 7. HTTP probe referencing /status
Add-KrHealthHttpProbe -Name 'ComponentStatus' -Url 'http://127.0.0.1:5002/status' -Tags 'remote', 'self' -Timeout '00:00:02'

## 8. Health endpoint
Add-KrHealthEndpoint -Pattern '/healthz' -DefaultTags 'self', 'remote'

## 9. Start server
Start-KrServer
