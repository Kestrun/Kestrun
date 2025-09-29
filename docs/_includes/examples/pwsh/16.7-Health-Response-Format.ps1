<#
    Sample:  Health Response Format
    Purpose: Shows how to configure the health endpoint to return YAML, XML, or JSON responses.
    File:    16.7-Health-Response-Format.ps1
    Notes:   Demonstrates ResponseContentType parameter options with multiple probes.
#>

## 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

## 2. Server
New-KrServer -Name 'Health Response Format Demo'

## 3. Listener (port 5000)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

## 4. Runtime
Add-KrPowerShellRuntime

## 5. Add some diverse probes for demonstration
Add-KrHealthProbe -Name 'Database' -Tags 'core', 'data' -ScriptBlock {
    # Simulate database connectivity check
    $connectionTime = Get-Random -Minimum 10 -Maximum 50
    $data = @{ connectionTimeMs = $connectionTime; server = 'localhost:5432' }
    New-KrProbeResult 'Healthy' "Connected in ${connectionTime}ms" -Data $data
}

Add-KrHealthProbe -Name 'Cache' -Tags 'core', 'perf' -ScriptBlock {
    # Simulate cache health check
    $hitRatio = [math]::Round((Get-Random -Minimum 85 -Maximum 98) / 100.0, 2)
    $data = @{ hitRatio = $hitRatio; entries = 1247 }
    $status = if ($hitRatio -lt 0.8) { 'Degraded' } else { 'Healthy' }
    New-KrProbeResult $status "Hit ratio: $($hitRatio * 100)%" -Data $data
}

Add-KrHealthProbe -Name 'Disk Space' -Tags 'infra' -ScriptBlock {
    # Simulate disk space check
    $freePercent = Get-Random -Minimum 25 -Maximum 85
    $status = if ($freePercent -lt 10) { 'Unhealthy' } elseif ($freePercent -lt 20) { 'Degraded' } else { 'Healthy' }
    $data = @{ freePercent = $freePercent; totalGB = 500; freeGB = [math]::Round($freePercent * 5, 1) }
    New-KrProbeResult $status "Free space: $freePercent%" -Data $data
}

## 6. Enable configuration
#Enable-KrConfiguration

## 7. Health endpoint with different response formats (uncomment one at a time to test)
# JSON (default)
Add-KrHealthEndpoint -Pattern '/healthz' -DefaultTags 'core' -ResponseContentType Auto
Enable-KrConfiguration
## 8. Start server
Start-KrServer
