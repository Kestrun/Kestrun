param()
Describe 'Example 5.5-Sinks-Advanced' -Tag 'Tutorial', 'Logging', 'Sinks' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '5.5-Sinks-Advanced.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /log returns ok text' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/log" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'ok'
    }
}
