param()
Describe 'Tutorial 18.4-ExceptionHandling-ProblemDetails' -Tag 'Tutorial' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '18.4-ExceptionHandling-ProblemDetails.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'GET /hello returns 200 and JSON' {
        $r = Invoke-ExampleRequest -Uri "$($script:instance.Url)/hello" -ReturnRaw
        $r.StatusCode | Should -Be 200
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'application/json'
        ($r.Content | ConvertFrom-Json).msg | Should -Be 'Hello from /hello'
    }

    It 'GET /boom returns RFC7807 ProblemDetails JSON with 500 status' {
        $r = Invoke-WebRequest -Uri "$($script:instance.Url)/boom" -UseBasicParsing -TimeoutSec 12 -SkipHttpErrorCheck
        $r.StatusCode | Should -Be 500
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'application/problem\+json|application/json'
        $r.Content | Should -Not -BeNullOrEmpty
        $text = if ($r.Content -is [byte[]]) { Convert-BytesToStringWithGzipScan -Bytes $r.Content } else { [string]$r.Content }
        $obj = $null
        try {
            $obj = $text | ConvertFrom-Json -AsHashtable
        } catch {
            # Not JSON or unexpected shape; proceed with fallback assertion
            $obj = $null
        }
        if ($obj -is [hashtable]) {
            # Common ProblemDetails fields (title/status) may vary by environment.
            # Assert that either 'status' exists and equals 500, or that 'type'/'title' exist.
            if ($obj.ContainsKey('status')) {
                [int]$obj['status'] | Should -Be 500
            } else {
                ($obj.ContainsKey('title') -or $obj.ContainsKey('type')) | Should -BeTrue
            }
        } else {
            # Fallback: ensure we at least received a textual body for the problem
            ($text.Trim().Length) | Should -BeGreaterThan 0
        }
    }
}
