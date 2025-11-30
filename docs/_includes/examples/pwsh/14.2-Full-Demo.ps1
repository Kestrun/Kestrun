<#
    14.1 Full Demo (moved from 7.9)
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault | Out-Null
$certPath = Join-Path $PSScriptRoot 'fulldemo.pfx'
if (-not (Test-Path $certPath)) {
    $demoPwd = Read-Host -Prompt 'Enter temporary password for fulldemo.pfx (dev only)' -AsSecureString
    New-KrSelfSignedCertificate -DnsName 'localhost' -Path $certPath -Password $demoPwd | Out-Null
} else {
    $demoPwd = Read-Host -Prompt 'Enter existing fulldemo.pfx password' -AsSecureString
}
$srv = New-KrServer -Name 'Full Demo Server' -PassThru
Add-KrEndpoint -Port $Port -IPAddress $IPAddress
Add-KrEndpoint -Port ($Port + 433) -IPAddress $IPAddress -CertPath $certPath -CertPassword $demoPwd
if ($IsWindows) { Add-KrNamedPipeListener -PipeName 'kestrun.full.pipe' }

Enable-KrConfiguration
Set-KrServerLimit -MaxRequestBodySize 2097152 -MaxConcurrentConnections 100 | Out-Null
Set-KrServerOptions -DenyServerHeader -MaxRunspaces 8 -MinRunspaces 2 | Out-Null
Add-KrMapRoute -Verbs Get -Pattern '/health' -ScriptBlock { Write-KrJsonResponse @{ ok = $true } }
Add-KrMapRoute -Verbs Get -Pattern '/version' -ScriptBlock { Write-KrJsonResponse @{ version = '1.0'; time = (Get-Date) } }
Start-KrServer -Server $srv -NoWait | Out-Null
Write-Host 'Demo running. Press Enter to stop...'
[void][Console]::ReadLine()
Stop-KrServer -Server $srv
Remove-KrServer -Name $srv.ApplicationName
