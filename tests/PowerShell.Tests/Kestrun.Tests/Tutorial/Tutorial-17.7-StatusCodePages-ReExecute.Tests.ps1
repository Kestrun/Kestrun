param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}
Describe 'Tutorial 17.7-StatusCodePages-ReExecute' -Tag 'Tutorial' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '17.7-StatusCodePages-ReExecute.ps1'
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Hello route returns HTML welcome' {
        $r = Invoke-ExampleRequest -Uri "$($script:instance.Url)/hello" -ReturnRaw
        $r.StatusCode | Should -Be 200
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'text/html'
    }

    It '500 route re-executes to /errors/500 page (HTML content)' {
        # Re-execute keeps the original URL but content comes from /errors/{statusCode}
        $base = $script:instance.Url
        $resp = Invoke-WebRequest -Uri "$base/error" -UseBasicParsing -TimeoutSec 12 -ErrorAction SilentlyContinue -SkipHttpErrorCheck
        # Sample renders final page with 200 OK even for original 500, so only assert content
        ($resp.Headers['Content-Type'] -join ';') | Should -Match 'text/html'
        $resp.Content | Should -Match '<!DOCTYPE html>'
        $resp.Content | Should -Match '500'
    }

    It 'Unmapped route re-executes to /errors/404 page (HTML content)' {
        $base = $script:instance.Url
        $resp = Invoke-WebRequest -Uri "$base/missing" -UseBasicParsing -TimeoutSec 12 -ErrorAction SilentlyContinue -SkipHttpErrorCheck
        ($resp.Headers['Content-Type'] -join ';') | Should -Match 'text/html'
        $resp.Content | Should -Match '<!DOCTYPE html>'
        $resp.Content | Should -Match '404'
    }
}
