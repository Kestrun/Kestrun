param()
Describe 'Tutorial 18.3-ExceptionHandling-CSharp' -Tag 'Tutorial' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '18.3-ExceptionHandling-CSharp.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /hello returns JSON 200' {
        $r = Invoke-ExampleRequest -Uri "$($script:instance.Url)/hello" -ReturnRaw
        $r.StatusCode | Should -Be 200
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'application/json'
        ($r.Content | ConvertFrom-Json).msg | Should -Be 'Hello from /hello'
    }

    It 'GET /fail returns 500 handled by C# exception handler' {
        $r = Invoke-WebRequest -Uri "$($script:instance.Url)/fail" -UseBasicParsing -TimeoutSec 12 -SkipHttpErrorCheck
        $r.StatusCode | Should -Be 500
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'application/json'
        $obj = $r.Content | ConvertFrom-Json
        $obj.error | Should -BeTrue
        $obj.message | Should -Match 'Handled by C# exception handler'
        $obj.path | Should -Be '/fail'
        $obj.method | Should -Be 'GET'
    }
}
