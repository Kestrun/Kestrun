param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 8.10 Authentication (GitHub OAuth)' -Tag 'Tutorial', 'Auth', 'Slow' {
    BeforeAll {
        $script:oldGitHubClientId = $env:GITHUB_CLIENT_ID
        $script:oldGitHubClientSecret = $env:GITHUB_CLIENT_SECRET

        if ([string]::IsNullOrWhiteSpace($env:GITHUB_CLIENT_ID)) {
            $env:GITHUB_CLIENT_ID = 'test-client-id'
        }

        if ([string]::IsNullOrWhiteSpace($env:GITHUB_CLIENT_SECRET)) {
            $env:GITHUB_CLIENT_SECRET = 'test-client-secret'
        }

        $script:instance = Start-ExampleScript -Name '8.10-GitHubAuthentication.ps1' -EnvironmentVariables @(
            'GITHUB_CLIENT_ID',
            'GITHUB_CLIENT_SECRET'
        )
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }

        $env:GITHUB_CLIENT_ID = $script:oldGitHubClientId
        $env:GITHUB_CLIENT_SECRET = $script:oldGitHubClientSecret
    }

    It 'GET / returns public GitHub auth landing page' {
        $result = Invoke-TestRequest -Uri "$($script:instance.Url)/" -SkipCertificateCheck -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Headers.'Content-Type' | Should -Be 'text/html; charset=utf-8'
        $result.Content | Should -Match 'GitHub|OAuth|Login|Sign\s*In'
    }

    It 'GET /github/me challenges unauthenticated users' {
        $result = Get-HttpHeadersRaw -Uri "$($script:instance.Url)/github/me" -Insecure -AsHashtable
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -BeIn @(302, 401)
    }

    It 'GET /github/login challenges unauthenticated users' {
        $result = Get-HttpHeadersRaw -Uri "$($script:instance.Url)/github/login" -Insecure -AsHashtable
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -BeIn @(302, 401)
    }
}
