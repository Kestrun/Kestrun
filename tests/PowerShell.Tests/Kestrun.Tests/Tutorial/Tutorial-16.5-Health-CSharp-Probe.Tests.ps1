param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}
Describe 'Example 16.5-Health-CSharp-Probe' -Tag 'Tutorial', 'Health' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '16.5-Health-CSharp-Probe.ps1'
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /healthz includes RandomCSharp probe with dynamic status' {
        $resp = Invoke-WebRequest -Uri "$( $script:instance.Url)/healthz" -UseBasicParsing -TimeoutSec 8 -Method Get -SkipHttpErrorCheck
        ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 503) | Should -BeTrue
        $json = $resp.Content | ConvertFrom-Json
        $probes = @()
        foreach ($name in 'probes', 'checks', 'results') { if ($json.PSObject.Properties.Name -contains $name -and $json.$name) { $probes += $json.$name } }
        $rc = $probes | Where-Object { $_.name -eq 'RandomCSharp' }
        $rc | Should -Not -BeNullOrEmpty
        $rc[0].statusText | Should -Match '^(healthy|degraded|unhealthy)$'
        if ($rc[0].PSObject.Properties.Name -contains 'data') {
            $dataProp = $rc[0].data
            if ($dataProp) { ($dataProp.PSObject.Properties.Name -contains 'value') | Should -BeTrue }
        }
    }
}
