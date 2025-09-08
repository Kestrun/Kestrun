<#
    13.2 Server Options Example (moved from 7.5)
#>
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null
New-KrServer -Name 'Server Options'
Add-KrListener -Port 5001 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime
Enable-KrConfiguration
Set-KrServerOptions -DenyServerHeader -DisableResponseHeaderCompression -MaxRunspaces 8 -MinRunspaces 2 | Out-Null
Add-KrMapRoute -Verbs Get -Pattern '/meta' -ScriptBlock {
    $srv = Get-KrServer
    Write-KrJsonResponse @{ host = $env:COMPUTERNAME; runspaces = $srv.Options.MaxRunspaces }
}
Start-KrServer
