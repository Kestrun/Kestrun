param()
Describe 'Example 5.2-Multiple-Loggers-Levels' -Tag 'Tutorial', 'Logging' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '5.2-Multiple-Loggers-Levels.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /info returns info text' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/info" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'info'
    }

    It 'GET /debug returns debug text' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/debug" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'debug'
    }

    It 'GET /audit returns audit text' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/audit" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'audit'
    }
}
