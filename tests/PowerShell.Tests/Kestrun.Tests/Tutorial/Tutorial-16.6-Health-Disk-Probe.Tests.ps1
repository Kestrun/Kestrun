param()
Describe 'Example 16.6-Health-Disk-Probe' -Tag 'Tutorial', 'Health' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '16.6-Health-Disk-Probe.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /healthz includes disk probe data' {
        $resp = Invoke-WebRequest -Uri "$( $script:instance.Url)/healthz" -UseBasicParsing -TimeoutSec 8 -Method Get -SkipHttpErrorCheck
        ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 503) | Should -BeTrue
        $json = $resp.Content | ConvertFrom-Json
        $probes = @()
        foreach ($name in 'probes','checks','results') { if ($json.PSObject.Properties.Name -contains $name -and $json.$name) { $probes += $json.$name } }
        $disk = $probes | Where-Object { $_.name -eq 'disk' }
        $disk | Should -Not -BeNullOrEmpty
        $disk[0].statusText | Should -Match '^(healthy|degraded|unhealthy)$'
        if ($disk[0].data) { ($disk[0].data.PSObject.Properties.Name -contains 'freePercent') | Should -BeTrue }
    }
}
