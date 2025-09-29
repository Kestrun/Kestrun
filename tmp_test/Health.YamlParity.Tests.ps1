<#
Validates that numeric probe data remains numeric in YAML health response.
Strategy:
  * Spin up server with YAML response preference.
  * Retrieve raw YAML via Invoke-WebRequest (Invoke-RestMethod would try to parse JSON only; YAML stays string anyway but WebRequest gives us .Content).
  * Parse with ConvertFrom-Yaml (PowerShell 7+ builtin) then assert numeric node types.
  * Mirror JSON numeric test semantics (intVal integer, floatVal floating point).
#>

Import-Module "$PSScriptRoot/../../src/PowerShell/Kestrun/Kestrun.psd1" -Force

New-KrServer -Name 'YamlParity'
Add-KrListener -Port 5016 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime

Add-KrHealthProbe -Name 'NumProbe' -ScriptBlock {
    New-KrProbeResult Healthy 'OK' -Data @{ intVal = 42; floatVal = 12.5 }
}

Enable-KrConfiguration
Add-KrHealthEndpoint -Pattern '/healthz' -ResponseContentType Yaml
Start-KrServer

try {
    Start-Sleep -Milliseconds 250
    $resp = Invoke-WebRequest -Uri 'http://localhost:5016/healthz' -Headers @{ Accept = 'application/yaml' }
    if (-not $resp -or -not $resp.Content) { throw 'No YAML response content received.' }

    $yamlText = $resp.Content
    $parsed = ConvertFrom-Yaml -Yaml $yamlText
    # Expect top-level with status/probes etc.
    $probe = $parsed.probes | Where-Object { $_.name -eq 'NumProbe' }
    if (-not $probe) { throw 'Probe NumProbe not found in YAML response.' }

    $intVal = $probe.data.intVal
    $floatVal = $probe.data.floatVal

    if (-not ($intVal -is [int] -or $intVal -is [long])) { throw "intVal not numeric integer: $($intVal.GetType().FullName) value=$intVal" }
    if (-not ($floatVal -is [double] -or $floatVal -is [float])) { throw "floatVal not floating numeric: $($floatVal.GetType().FullName) value=$floatVal" }
}
finally {
    Stop-KrServer -Name 'YamlParity' -Force -ErrorAction SilentlyContinue | Out-Null
}
