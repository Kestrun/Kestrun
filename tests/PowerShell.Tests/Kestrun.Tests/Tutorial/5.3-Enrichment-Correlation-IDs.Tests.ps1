param()
Describe 'Example 5.3-Enrichment-Correlation-IDs' -Tag 'Tutorial', 'Logging', 'Correlation' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '5.3-Enrichment-Correlation-IDs.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Correlation ID routes respond with 200' { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; Test-ExampleRouteSet -Instance $script:instance }

    It 'Correlation route returns correlation id in body' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $url = "http://127.0.0.1:$($script:instance.Port)/ps-correlation"
        $resp = Invoke-ExampleRequest -Uri $url -ReturnRaw -RetryCount 2
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Match 'ps-correlation: [0-9a-f]{32}'
    }
}
