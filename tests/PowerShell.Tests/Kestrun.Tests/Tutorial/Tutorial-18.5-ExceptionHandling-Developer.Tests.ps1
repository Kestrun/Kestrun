param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Tutorial 18.5-ExceptionHandling-Developer' -Tag 'Tutorial' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '18.5-ExceptionHandling-Developer.ps1'
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /hello returns 200 and JSON' {
        $r = Invoke-ExampleRequest -Uri "$($script:instance.Url)/hello" -ReturnRaw
        $r.StatusCode | Should -Be 200
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'application/json'
        ($r.Content | ConvertFrom-Json).msg | Should -Be 'Hello from /hello'
    }

    It 'GET /throw returns 500 and HTML developer exception page' {
        $r = Invoke-WebRequest -Uri "$($script:instance.Url)/throw" -UseBasicParsing -TimeoutSec 15 -SkipHttpErrorCheck
        $r.StatusCode | Should -Be 500
        $ct = ($r.Headers['Content-Type'] -join ';')
        if ($ct) { $ct | Should -Match 'text\/(html|plain)' }
        # Body should include exception details or HTML markers
        $body = if ($r.Content -is [byte[]]) { Convert-BytesToStringWithGzipScan -Bytes $r.Content } else { [string]$r.Content }
        ($body -match 'InvalidOperationException' -or $body -match '<!DOCTYPE html>' -or $body -match '<html') | Should -BeTrue
    }
}
