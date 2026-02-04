param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Tutorial 17.4-StatusCodePages-CustomCSharp' -Tag 'Tutorial' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '17.4-StatusCodePages-CustomCSharp.ps1'
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Hello route returns JSON success' {
        $r = Invoke-ExampleRequest -Uri "$($script:instance.Url)/hello" -ReturnRaw
        $r.StatusCode | Should -Be 200
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'application/json'
        $r.Content | Should -Match '"message"\s*:\s*"Hello, World!"'
    }

    It 'Custom C# handler returns JSON for 500 with description' {
        $base = $script:instance.Url
        $r = Invoke-WebRequest -Uri "$base/error" -UseBasicParsing -TimeoutSec 12 -ErrorAction SilentlyContinue -SkipHttpErrorCheck
        $r.StatusCode | Should -Be 500
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'application/json'
        $r.Content | Should -Match '"error"\s*:\s*true'
        $r.Content | Should -Match '"statusCode"\s*:\s*500'
        $r.Content | Should -Match 'Internal server error|Something went wrong|Internal server error'
    }

    It 'Unmapped route returns JSON 404 via custom C# handler' {
        $base = $script:instance.Url
        $r = Invoke-WebRequest -Uri "$base/missing" -UseBasicParsing -TimeoutSec 12 -ErrorAction SilentlyContinue -SkipHttpErrorCheck
        $r.StatusCode | Should -Be 404
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'application/json'
        $r.Content | Should -Match '"error"\s*:\s*true'
        $r.Content | Should -Match '"statusCode"\s*:\s*404'
    }
}
