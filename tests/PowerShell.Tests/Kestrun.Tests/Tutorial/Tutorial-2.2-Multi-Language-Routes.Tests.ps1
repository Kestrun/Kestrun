param()
Describe 'Example 2.2-Multi-Language-Routes' -Tag 'Tutorial' {
    BeforeAll {. (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '2.2-Multi-Language-Routes.ps1' }
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
    It 'hello-vbnet returns Hello, World!' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/hello-vbnet" -UseBasicParsing -TimeoutSec 6 -Method Get
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'Hello, World!'
    }
}
