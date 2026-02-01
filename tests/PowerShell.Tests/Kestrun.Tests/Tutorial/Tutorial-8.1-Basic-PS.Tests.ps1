param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 8.x Authentication (Basic-PS etc.)' -Tag 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '8.1-Basic-PS.ps1'
        # Precompute Basic auth header value for tests
        # Username: admin, Password: password
        $creds = 'admin:password'
        $script:basic = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($creds)) }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'ps/hello in PowerShell' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/ps/hello" -SkipCertificateCheck -Headers @{Authorization = $script:basic }
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Be 'Hello, admin!'
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }
}
