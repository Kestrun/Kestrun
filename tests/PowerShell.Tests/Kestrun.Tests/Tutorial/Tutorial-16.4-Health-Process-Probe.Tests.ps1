param()
Describe 'Example 16.4-Health-Process-Probe' -Tag 'Tutorial', 'Health' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '16.4-Health-Process-Probe.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /healthz returns DotNetInfo probe' {
        $resp = Invoke-WebRequest -Uri "$( $script:instance.Url)/healthz" -UseBasicParsing -TimeoutSec 12 -Method Get -SkipHttpErrorCheck
        ($resp.StatusCode -eq 200 -or $resp.StatusCode -eq 503) | Should -BeTrue
        $json = $null
        try { $json = $resp.Content | ConvertFrom-Json -ErrorAction Stop } catch { throw 'Process probe health response not JSON' }
        $probes = @()
        foreach ($name in 'probes','checks','results') { if ($json.PSObject.Properties.Name -contains $name -and $json.$name) { $probes += $json.$name } }
        $probes.Count | Should -BeGreaterOrEqual 2
        ($probes | Where-Object { $_.name -eq 'DotNetInfo' }).Count | Should -Be 1
    }
}
