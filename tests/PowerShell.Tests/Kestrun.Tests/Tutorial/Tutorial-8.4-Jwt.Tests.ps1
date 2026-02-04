param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 8.4 Authentication (JWT)' -Tag 'Tutorial', 'Slow' {
    BeforeAll { $script:instance = Start-ExampleScript -Name '8.4-Jwt.ps1' }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'JWT token generation' {
        $creds = 'admin:password'
        $basic = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($creds))
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/token/new" -SkipCertificateCheck -Headers @{ Authorization = $basic } -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Not -BeNullOrEmpty
        $result.Headers.'Content-Type' | Should -Be 'application/json; charset=utf-8'
        $jsonContent = $result.Content | ConvertFrom-Json
        $jsonContent.access_token | Should -Not -BeNullOrEmpty
        $script:token = $jsonContent.access_token
    }

    It 'jwt authentication Hello in PowerShell' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/jwt/hello" -SkipCertificateCheck -Headers @{ Authorization = "Bearer $script:token" } -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Not -BeNullOrEmpty
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        $result.Content | Should -Be 'JWT Hello admin'
    }

    It 'JWT token renewal' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/token/renew" -SkipCertificateCheck -Headers @{ Authorization = "Bearer $script:token" } -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Not -BeNullOrEmpty
        $result.Headers.'Content-Type' | Should -Be 'application/json; charset=utf-8'
        $jsonContent = $result.Content | ConvertFrom-Json
        $script:token2 = $jsonContent.access_token
        $script:token2 | Should -Not -BeNullOrEmpty
    }

    It 'jwt authentication Hello in PowerShell with renewed token' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/jwt/hello" -SkipCertificateCheck -Headers @{ Authorization = "Bearer $script:token2" } -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Not -BeNullOrEmpty
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
        $result.Content | Should -Be 'JWT Hello admin'
    }
}
