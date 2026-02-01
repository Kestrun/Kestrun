param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Tutorial 17.2-StatusCodePages-Options' -Tag 'Tutorial' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '17.2-StatusCodePages-Options.ps1'
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Hello route returns expected response' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/hello" -UseBasicParsing -TimeoutSec 8
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Be 'hello'
    }

    It 'NotFound route returns default text error page (404)' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/notfound" -UseBasicParsing -TimeoutSec 8 -ErrorAction SilentlyContinue -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 404
        # Default middleware renders a plain text line with the status info
        $resp.Content | Should -BeLike 'Status Code: 404; Not Found*'
    }

    It 'Unmapped route returns default text error page (404)' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/missing" -UseBasicParsing -TimeoutSec 8 -ErrorAction SilentlyContinue -SkipHttpErrorCheck
        $resp.StatusCode | Should -Be 404
        $resp.Content | Should -BeLike 'Status Code: 404; Not Found*'
    }
}
