# Validates plain text health output option
Import-Module "$PSScriptRoot/../../src/PowerShell/Kestrun/Kestrun.psd1" -Force

New-KrServer -Name 'TxtHealth'
Add-KrEndpoint -Port 5020 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime

Add-KrHealthProbe -Name 'QuickProbe' -ScriptBlock {
    New-KrProbeResult Healthy 'All good' -Data @{ latencyMs = 5 }
}

Enable-KrConfiguration
Add-KrHealthEndpoint -Pattern '/healthz' -ResponseContentType Text
Start-KrServer -CloseLogsOnExit


Start-Sleep -Milliseconds 250
$resp = Invoke-WebRequest -Uri 'http://localhost:5020/healthz' -Headers @{ Accept = 'text/plain' }
if (-not $resp.Content) { throw 'No text content received.' }
$content = $resp.Content -split "`n"
if (-not ($content | Where-Object { $_ -match '^Status: ' })) { throw 'Missing Status line' }
if (-not ($content | Where-Object { $_ -match '^Probes:' })) { throw 'Missing Probes header' }
if (-not ($content | Where-Object { $_ -match 'name=QuickProbe' })) { throw 'Missing probe line' }
if (-not ($content | Where-Object { $_ -match 'latencyMs=5' })) { throw 'Missing probe data key/value' }
