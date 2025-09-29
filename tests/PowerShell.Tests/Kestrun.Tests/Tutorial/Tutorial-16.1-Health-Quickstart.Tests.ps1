param()
Describe 'Example 16.1-Health-Quickstart' -Tag 'Tutorial', 'Health' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '16.1-Health-Quickstart.ps1' }
    AfterAll { if ($script:instance) {
        Stop-ExampleScript -Instance $script:instance } }

    It 'GET /ping returns expected JSON payload' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/ping" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $json = $resp.Content | ConvertFrom-Json
        $json.status | Should -Be 'Healthy'
        if ($json.PSObject.Properties.Name -contains 'description') {
            ($json.description -as [string]).ToLower() | Should -Match 'ping'
        }
    }

    It 'GET /healthz returns Healthy with Self and Ping probes' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/healthz" -UseBasicParsing -TimeoutSec 6 -Method Get -SkipHttpErrorCheck
        ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 503) | Should -BeTrue
        $json = $resp.Content | ConvertFrom-Json
        $json.statusText -eq 'Healthy' -or $json.statusText -eq 'Degraded' | Should -BeTrue

        $probeCollections = @()
        foreach ($name in 'probes', 'checks', 'results') {
            if ($json.PSObject.Properties.Name -contains $name -and $null -ne $json.$name) { $probeCollections += $json.$name }
        }
        $probes = @($probeCollections)
        $probes.Count | Should -BeGreaterOrEqual 2

        $probeNames = $probes | ForEach-Object { if ($_ -is [string]) { $_ } else { $_.name } } | Where-Object { $_ }
        $probeNames | Should -Contain 'Self'
        $probeNames | Should -Contain 'Ping'

        foreach ($p in $probes) { $p.statusText -eq 'Healthy' -or $p.statusText -eq 'Degraded' | Should -BeTrue}  
    }
}
