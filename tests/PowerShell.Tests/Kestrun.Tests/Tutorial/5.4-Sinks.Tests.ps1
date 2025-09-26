param()
Describe 'Example 5.4-Sinks' -Tag 'Tutorial', 'Logging', 'Sinks' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '5.4-Sinks.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /text returns text' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/text" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'text'
    }

    It 'GET /json returns json text' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/json" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'json'
    }
}
