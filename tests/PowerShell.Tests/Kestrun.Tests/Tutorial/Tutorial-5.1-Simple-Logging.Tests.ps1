param()
Describe 'Example 5.1-Simple-Logging' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '5.1-Simple-Logging.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'hello-powershell returns Hello, World!' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/hello-powershell" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'Hello, World!'
    }

    It 'hello-csharp returns Hello, World!' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/hello-csharp" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'Hello, World!'
    }
}
