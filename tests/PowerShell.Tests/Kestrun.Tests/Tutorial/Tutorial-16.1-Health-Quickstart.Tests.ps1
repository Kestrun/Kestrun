param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 16.1-Health-Quickstart' -Tag 'Tutorial', 'Health' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '16.1-Health-Quickstart.ps1'
    }
    AfterAll {
        if ($script:instance) {

            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /ping returns expected JSON payload' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/ping" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $json = $resp.Content | ConvertFrom-Json
        $json.status | Should -Be 'Healthy'
        if ($json.PSObject.Properties.Name -contains 'description') {
            ($json.description -as [string]).ToLower() | Should -Match 'ping'
        }
    }

    It 'GET /healthz returns Healthy (or Degraded) with Self and Ping probes' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/healthz" -UseBasicParsing -TimeoutSec 8 -Method Get -SkipHttpErrorCheck
        # Accept 200 (healthy) or 503 (unhealthy because TreatDegradedAsUnhealthy or transient) HTTP codes
        ($resp.StatusCode -in 200, 503) | Should -BeTrue -Because 'Health endpoint should return 200 or 503'
        $raw = $resp.Content
        $json = $raw | ConvertFrom-Json

        # Determine overall status property dynamically
        $statusProp = @('statusText', 'status', 'overallStatus', 'state') | Where-Object { $json.PSObject.Properties.Name -contains $_ } | Select-Object -First 1
        $effectiveStatus = if ($statusProp) { $json.$statusProp } else { $null }
        if (-not $effectiveStatus) { Write-Host 'Health JSON keys:' ($json.PSObject.Properties.Name -join ', '); Write-Host 'Raw JSON:' $raw }
        $normalizedStatus = ($effectiveStatus -as [string])
        if ($normalizedStatus) { $normalizedStatus = $normalizedStatus.Trim() }
        $acceptable = @('Healthy', 'Degraded', 'Unhealthy', 'unhealthy', 'degraded', 'healthy')
        if ($normalizedStatus -notin $acceptable) {
            Write-Host "Unexpected overall status: '$normalizedStatus' (raw: '$effectiveStatus') keys:" ($json.PSObject.Properties.Name -join ', ')
            Write-Host 'Raw JSON:' $raw
        }
        ($normalizedStatus -in $acceptable) | Should -BeTrue -Because "Expected overall health status in set (Healthy|Degraded|Unhealthy) but found '$normalizedStatus'"

        # Collect probe arrays under any supported property names
        $probeCollections = @()
        foreach ($name in 'probes', 'checks', 'results', 'entries') {
            if ($json.PSObject.Properties.Name -contains $name -and $null -ne $json.$name) { $probeCollections += $json.$name }
        }
        $probes = @($probeCollections)
        if (-not $probes -or $probes.Count -lt 2) { Write-Host 'Health JSON (probe insufficient) raw:' $raw }
        $probes.Count | Should -BeGreaterOrEqual 2 -Because 'Expect at least two probes (Self and Ping)'

        $probeNames = $probes |
            ForEach-Object {
                if ($_ -is [string]) { $_ }
                elseif ($_.PSObject.Properties.Name -contains 'name') { $_.name }
                elseif ($_.PSObject.Properties.Name -contains 'id') { $_.id }
            } |
            Where-Object { $_ }
        $probeNames | Should -Contain 'Self'
        $probeNames | Should -Contain 'Ping'

        foreach ($p in $probes) {
            $pStatusProp = @('statusText', 'status', 'state') | Where-Object { $p.PSObject.Properties.Name -contains $_ } | Select-Object -First 1
            $pStatus = if ($pStatusProp) { $p.$pStatusProp } else { $null }
            if (-not $pStatus) { continue }
            if ($pStatus) {
                ($pStatus -in 'Healthy', 'Degraded', 'Unhealthy', 'healthy', 'degraded', 'unhealthy') | Should -BeTrue -Because "Probe '$($p.name)' expected Healthy/Degraded/Unhealthy (found '$pStatus')"
            }
        }
    }
}
