param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 8.x Authentication (Cookies)' -Tag 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '8.5-Cookies.ps1'
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Cookies login' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/cookies/login" -SkipCertificateCheck `
            -Method Post -Body @{ username = 'admin'; password = 'secret' } -SessionVariable lauthSession -SkipHttpErrorCheck
        $script:authSession = $lauthSession
        $script:authSession | Should -Not -BeNullOrEmpty
        $script:authSession.Cookies.Count | Should -Be 1
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Not -BeNullOrEmpty
        $result.Headers.'Content-Type' | Should -Be 'application/json; charset=utf-8'
        $jsonContent = $result.Content | ConvertFrom-Json
        $jsonContent.success | Should -Be $true
    }

    It 'Cookies authentication Hello in PowerShell' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/cookies/hello" -SkipCertificateCheck -WebSession $script:authSession -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Be 'Welcome, admin! You are authenticated by Cookies Authentication.'
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }

    It 'Cookies logout' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/cookies/logout" -SkipCertificateCheck -WebSession $script:authSession -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Not -BeNullOrEmpty
        $result.Headers.'Content-Type' | Should -Be 'text/html; charset=utf-8'
    }

    It 'Failed Cookies authentication Hello after logout' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/cookies/hello" -SkipCertificateCheck -WebSession $script:authSession -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Content | Should -BeLike '*<!DOCTYPE html>*'
        $result.Headers.'Content-Type' | Should -Be 'text/html; charset=utf-8'
    }
}

