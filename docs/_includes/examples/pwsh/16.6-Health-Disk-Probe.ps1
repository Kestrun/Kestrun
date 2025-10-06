<#
    Sample:  Disk Probe
    Purpose: Shows the auto-registered disk probe and how to override with custom thresholds.
    File:    16.6-Health-Disk-Probe.ps1
    Notes:   Demonstrates the use of a custom disk probe with specific health thresholds.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

## 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

## 2. Server
New-KrServer -Name 'Health Disk Probe'

## 3. Listener (port 5000)
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

#
# Override disk probe by registering a custom one with same name
## 5. Override auto disk probe with custom logic
Add-KrHealthProbe -Name 'disk' -Tags 'self', 'infra' -ScriptBlock {
    # Simulated values for clarity in documentation (replace with real drive stats if desired)
    $total = 100GB
    $free = 12GB
    $percent = [math]::Round(($free / $total) * 100, 2)
    $status = if ($percent -lt 5) { 'Unhealthy' } elseif ($percent -lt 15) { 'Degraded' } else { 'Healthy' }
    $data = [Collections.Generic.Dictionary[string, object]]::new()
    $data['freePercent'] = $percent
    $data['totalBytes'] = $total
    $data['freeBytes'] = $free
    New-KrProbeResult $status "Free=$percent%" -Data $data
}

## 6. Enable configuration
Enable-KrConfiguration

## 7. Health endpoint
Add-KrHealthEndpoint -Pattern '/healthz' -DefaultTags 'self', 'infra'

## 8. Start server
Start-KrServer -CloseLogsOnExit
