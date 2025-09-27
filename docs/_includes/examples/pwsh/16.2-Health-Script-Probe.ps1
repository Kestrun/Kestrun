<#
 Script Probe Example
 Demonstrates adding a custom PowerShell script probe with additional data.
#>
## 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

## 2. Server
New-KrServer -Name 'Health Script Probe'

## 3. Listener (port 5000)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

## 4. Runtime
Add-KrPowerShellRuntime

## 5. Enable configuration
Enable-KrConfiguration

## 6. Script latency probe (random sleep -> classify)
Add-KrHealthProbe -Name 'LatencyCheck' -Tags 'self', 'perf' -ScriptBlock {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Milliseconds (Get-Random -Minimum 50 -Maximum 180)
    $sw.Stop()
    $elapsed = $sw.ElapsedMilliseconds
    $status = if ($elapsed -ge 170) { 'Unhealthy' } elseif ($elapsed -ge 130) { 'Degraded' } else { 'Healthy' }
    $data = [Collections.Generic.Dictionary[string, object]]::new()
    $data['elapsedMs'] = $elapsed
    New-KrProbeResult $status "Latency ${elapsed}ms" -Data $data
}

## 7. Health endpoint
Add-KrHealthEndpoint -Pattern '/healthz' -DefaultTags 'self' -ProbeTimeout '00:00:03'

## 8. Start server
Start-KrServer
