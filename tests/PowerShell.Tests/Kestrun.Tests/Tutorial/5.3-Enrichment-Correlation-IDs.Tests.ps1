param()
Describe 'Example 5.3-Enrichment-Correlation-IDs' -Tag 'Tutorial', 'Logging', 'Correlation' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '5.3-Enrichment-Correlation-IDs.ps1' }
    AfterAll {if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /ps-correlation returns correlation id' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/ps-correlation" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Match '^ps-correlation: [0-9a-f]{32}$'
    }

    It 'GET /csharp-correlation returns correlation id' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/csharp-correlation" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Match '^csharp-correlation: [0-9a-f]{32}$'
    }
}
