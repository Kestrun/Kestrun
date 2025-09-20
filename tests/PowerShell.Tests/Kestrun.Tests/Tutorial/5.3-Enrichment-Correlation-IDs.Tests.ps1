param()
Describe 'Example 5.3-Enrichment-Correlation-IDs' -Tag 'Tutorial','Logging','Correlation' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '5.3-Enrichment-Correlation-IDs.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Correlation ID routes respond with 200' { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; Test-ExampleRouteSet -Instance $script:instance }

    It 'Custom correlation header flows through (header or body echo)' {
        . "$PSScriptRoot/TutorialExampleTestHelper.ps1"
        $cid = [guid]::NewGuid().ToString()
        $url = "http://127.0.0.1:$($script:instance.Port)/"  # root or a known logging route
        $resp = Invoke-ExampleRequest -Uri $url -Headers @{ 'X-Correlation-ID' = $cid } -ReturnRaw -RetryCount 2
        $resp.StatusCode | Should -Be 200
        $echoed = $false
        if ($resp.Headers['X-Correlation-ID']) { ($resp.Headers['X-Correlation-ID'] -eq $cid) | Should -BeTrue; $echoed = $true }
        elseif ($resp.Content -like "*${cid}*") { $echoed = $true }
        $echoed | Should -BeTrue -Because 'Correlation ID should be present in headers or body.'
    }
}
