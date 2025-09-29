# Validates that numeric probe data remains numeric in JSON health response
# Arrange: start a minimal server with a single script probe

Import-Module "$PSScriptRoot/../../src/PowerShell/Kestrun/Kestrun.psd1" -Force

New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault
New-KrServer -Name 'NumericTest'
Add-KrListener -Port 5015 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime

Add-KrHealthProbe -Name 'NumProbe' -ScriptBlock {
    $data = @{ connectionTimeMs = 42; latencyMs = 12.5 }
    New-KrProbeResult Healthy 'OK' -Data $data
}
Enable-KrConfiguration
Add-KrHealthEndpoint -Pattern '/healthz' -ResponseContentType Json
Start-KrServer

try {
    Start-Sleep -Milliseconds 200
    $json = Invoke-RestMethod -Uri 'http://localhost:5015/healthz' -Headers @{ Accept = 'application/json' }
    $probe = $json.probes | Where-Object { $_.name -eq 'NumProbe' }
    if (-not $probe) { throw 'Probe result not found in response' }

    # Assert numeric types (PowerShell will deserialize numbers as Int64 / Double)
    if (-not ($probe.data.connectionTimeMs -is [int] -or $probe.data.connectionTimeMs -is [long])) { throw "connectionTimeMs not numeric: $($probe.data.connectionTimeMs.GetType().FullName)" }
    if (-not ($probe.data.latencyMs -is [double] -or $probe.data.latencyMs -is [float])) { throw "latencyMs not floating numeric: $($probe.data.latencyMs.GetType().FullName)" }
} finally {
    Stop-KrServer -Name 'NumericTest' -Force -ErrorAction SilentlyContinue | Out-Null
}
