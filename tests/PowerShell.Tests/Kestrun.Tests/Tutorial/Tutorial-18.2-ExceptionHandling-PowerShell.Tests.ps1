param()
Describe 'Tutorial 18.2-ExceptionHandling-PowerShell' -Tag 'Tutorial' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '18.2-ExceptionHandling-PowerShell.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /ok returns 200 text' {
        $r = Invoke-ExampleRequest -Uri "$($script:instance.Url)/ok" -ReturnRaw
        $r.StatusCode | Should -Be 200
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'text/plain'
        $r.Content.Trim() | Should -Be 'Everything is fine.'
    }

    It 'GET /oops returns 500 due to script throw' {
        # The sample throws in PowerShell route; no middleware catches PowerShell script exceptions by design
        $r = Invoke-WebRequest -Uri "$($script:instance.Url)/oops" -UseBasicParsing -TimeoutSec 12 -SkipHttpErrorCheck
        $r.StatusCode | Should -Be 500
        # Content-Type and body may be absent for thrown PS exceptions; assertion kept to status only
    }

    It 'GET /csharp-error is handled by exception middleware with JSON payload' {
        $r = Invoke-WebRequest -Uri "$($script:instance.Url)/csharp-error" -UseBasicParsing -TimeoutSec 12 -SkipHttpErrorCheck
        $r.StatusCode | Should -Be 500
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'application/json'
        $obj = $r.Content | ConvertFrom-Json
        $obj.error | Should -BeTrue
        $obj.message | Should -Match 'Handled by middleware exception handler'
    }
}
