param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 16.2-Health-Script-Probe' -Tag 'Tutorial', 'Health' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '16.2-Health-Script-Probe.ps1'
    }
    AfterAll { if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /healthz returns expected LatencyCheck probe' {
        $resp = Invoke-WebRequest -Uri "$( $script:instance.Url)/healthz" -UseBasicParsing -TimeoutSec 8 -Method Get -SkipHttpErrorCheck
        ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 503) | Should -BeTrue
        $json = $null
        try { $json = $resp.Content | ConvertFrom-Json -ErrorAction Stop } catch { throw "Health response not valid JSON: $($_.Exception.Message)" }
        $json.statusText | Should -Match '^(healthy|degraded|unhealthy)$'
        $probeCollections = @()
        foreach ($name in 'probes', 'checks', 'results') { if ($json.PSObject.Properties.Name -contains $name -and $null -ne $json.$name) { $probeCollections += $json.$name } }
        $probes = @($probeCollections)
        $probes.Count | Should -BeGreaterOrEqual 2
        $latency = $probes | Where-Object { $_.name -eq 'LatencyCheck' }
        $latency | Should -Not -BeNullOrEmpty
        $latency[0].statusText | Should -Match '^(healthy|degraded|unhealthy)$'
        # Latency data should expose elapsedMs
        if ($latency[0].data) { ($latency[0].data.PSObject.Properties.Name -contains 'elapsedMs') | Should -BeTrue }
    }
}
