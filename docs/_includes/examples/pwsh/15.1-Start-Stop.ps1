<#
    15.1 Start / Stop Patterns
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null
$srv = New-KrServer -Name 'Lifecycle Demo' -PassThru
Add-KrEndpoint -Port $Port -IPAddress $IPAddress
Add-KrPowerShellRuntime
Enable-KrConfiguration
Add-KrMapRoute -Verbs Get -Pattern '/health' -ScriptBlock {
    Write-KrLog -Level Debug -Message 'Health check'
    Write-KrJsonResponse -InputObject @{ status = 'healthy' }
}
Start-KrServer -Server $srv -NoWait | Out-Null
Write-KrLog -Level Information -Message 'Server started (non-blocking).'
Write-Host 'Server running. Press Enter to stop...'
[void][Console]::ReadLine()
Stop-KrServer -Server $srv
Write-KrLog -Level Information -Message 'Server stopped.'
Remove-KrServer -Name $srv.ApplicationName
