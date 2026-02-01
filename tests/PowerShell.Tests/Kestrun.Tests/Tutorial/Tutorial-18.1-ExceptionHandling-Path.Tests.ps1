param()
Describe 'Tutorial 18.1-ExceptionHandling-Path' -Tag 'Tutorial' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '18.1-ExceptionHandling-Path.ps1' }
    AfterAll { if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
 } }

    It 'GET /hello returns 200 and JSON' {
        $r = Invoke-ExampleRequest -Uri "$($script:instance.Url)/hello" -ReturnRaw
        $r.StatusCode | Should -Be 200
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'application/json'
        $obj = $r.Content | ConvertFrom-Json
        $obj.msg | Should -Be 'Hello from /hello'
    }

    It 'GET /throw re-executes to /error and returns 500 Problem JSON with original path' {
        $r = Invoke-WebRequest -Uri "$($script:instance.Url)/throw" -UseBasicParsing -TimeoutSec 12 -SkipHttpErrorCheck
        $r.StatusCode | Should -Be 500
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'application/json'
        $obj = $r.Content | ConvertFrom-Json
        $obj.error | Should -BeTrue
        $obj.message | Should -Match 'Request re-executed to /error'
        # On re-execution the effective request path is the handler path
        $obj.path | Should -Be '/error'
        $obj.method | Should -Be 'GET'
    }
}
