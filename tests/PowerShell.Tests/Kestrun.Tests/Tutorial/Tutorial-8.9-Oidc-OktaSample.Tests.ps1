param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 8.9 Authentication (OpenID Connect - Okta)' -Tag 'Tutorial', 'Auth', 'Slow' {
    BeforeAll {
        $script:oldOktaClientId = $env:OKTA_CLIENT_ID
        $script:oldOktaClientSecret = $env:OKTA_CLIENT_SECRET
        $script:oldOktaAuthority = $env:OKTA_AUTHORITY

        if ([string]::IsNullOrWhiteSpace($env:OKTA_CLIENT_ID)) {
            $env:OKTA_CLIENT_ID = 'test-client-id'
        }

        if ([string]::IsNullOrWhiteSpace($env:OKTA_CLIENT_SECRET)) {
            $env:OKTA_CLIENT_SECRET = 'test-client-secret'
        }

        if ([string]::IsNullOrWhiteSpace($env:OKTA_AUTHORITY)) {
            $env:OKTA_AUTHORITY = 'https://example.okta.com/oauth2/default'
        }

        $script:instance = Start-ExampleScript -Name '8.9-Oidc-OktaSample.ps1' -EnvironmentVariables @(
            'OKTA_CLIENT_ID',
            'OKTA_CLIENT_SECRET',
            'OKTA_AUTHORITY'
        )
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }

        $env:OKTA_CLIENT_ID = $script:oldOktaClientId
        $env:OKTA_CLIENT_SECRET = $script:oldOktaClientSecret
        $env:OKTA_AUTHORITY = $script:oldOktaAuthority
    }

    It 'GET / returns public login page when unauthenticated' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/" -SkipCertificateCheck -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Headers.'Content-Type' | Should -Be 'text/html; charset=utf-8'
        $result.Content | Should -Match 'Login|Sign\s*In|OIDC|Okta'
    }

    It 'GET /me returns unauthenticated profile payload without interactive login' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/me" -SkipCertificateCheck -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Headers.'Content-Type' | Should -Be 'application/json; charset=utf-8'

        $json = $result.Content | ConvertFrom-Json
        $json.authenticated | Should -Be $false
        $json.PSObject.Properties.Name | Should -Contain 'claims'
    }

    It 'GET /signout returns logout callback page' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/signout" -SkipCertificateCheck -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Headers.'Content-Type' | Should -Be 'text/html; charset=utf-8'
    }
}
