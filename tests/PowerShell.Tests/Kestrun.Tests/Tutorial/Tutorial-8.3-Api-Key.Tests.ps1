param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 8.3 Authentication (ApiKey)' -Tag 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '8.3-Api-Key.ps1'
        $script:headers = @{ 'X-Api-Key' = 'my-secret-api-key' }
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'key authentication Hello Simple mode' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key/simple/hello" -SkipCertificateCheck -Headers $script:headers
        $result.Content | Should -Be 'Simple Key OK'
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }

    It 'key authentication Hello in powershell' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key/ps/hello" -SkipCertificateCheck -Headers $script:headers
        $result.Content | Should -Be 'PS Key OK'
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }
    It 'key authentication Hello in CSharp' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key/cs/hello" -SkipCertificateCheck -Headers $script:headers
        $result.Content | Should -Be 'CS Key OK'
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }
}
