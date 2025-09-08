<#
    13.1 Server Limits Example (moved from 7.4)
#>
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null
New-KrServer -Name 'Server Limits'
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime
Enable-KrConfiguration
Set-KrServerLimit -MaxRequestBodySize 1048576 -MaxConcurrentConnections 50 -KeepAliveTimeoutSeconds 60 -MaxRequestHeadersTotalSize 16384 | Out-Null
Add-KrMapRoute -Verbs Get -Pattern '/info' -ScriptBlock { Write-KrJsonResponse @{ status = 'ok'; time = (Get-Date) } }
Start-KrServer
