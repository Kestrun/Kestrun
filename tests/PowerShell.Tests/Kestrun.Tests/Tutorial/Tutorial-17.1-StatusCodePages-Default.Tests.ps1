param()
Describe 'Tutorial 17.1-StatusCodePages-Default' -Tag 'Tutorial' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '17.1-StatusCodePages-Default.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Hello route returns expected response' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/hello" -UseBasicParsing -TimeoutSec 8
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'Hello, World!'
    }

    It 'Non-existent route returns default error page' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/notfound" -UseBasicParsing -TimeoutSec 8 -ErrorAction SilentlyContinue -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 404
        $resp.Content | Should -BeLike 'Status Code: 404; Not Found*'
    }

    It 'Unmapped route returns default error page' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/missing" -UseBasicParsing -TimeoutSec 8 -ErrorAction SilentlyContinue -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 404
        $resp.Content | Should -BeLike 'Status Code: 404; Not Found*'
    }
}
