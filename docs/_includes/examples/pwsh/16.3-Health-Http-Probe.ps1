<#
 HTTP Probe Example
 Demonstrates adding an HTTP health probe calling a local route.
#>
## 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

## 2. Server
New-KrServer -Name 'Health HTTP Probe'

## 3. Listener (port 5000)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

## 4. Runtime
Add-KrPowerShellRuntime

## 5. Enable configuration
Enable-KrConfiguration

## 6. Supporting /status route simulating downstream component
Add-KrMapRoute -Verbs Get -Pattern '/status' -ScriptBlock {
    Write-KrJsonResponse @{ status = 'Healthy'; description = 'Component OK'; version = '1.0.3' }
}

## 7. HTTP probe referencing /status
# The listener above is on port 5000, so target that; adjust if you intentionally host /status elsewhere.
Add-KrHealthHttpProbe -Name 'ComponentStatus' -Url 'http://127.0.0.1:5000/status' -Tags 'remote', 'self' -Timeout '00:00:02'

## 8. Health endpoint
Add-KrHealthEndpoint -Pattern '/healthz' -DefaultTags 'self', 'remote' -ResponseContentType Xml

## 9. Start server
Start-KrServer
