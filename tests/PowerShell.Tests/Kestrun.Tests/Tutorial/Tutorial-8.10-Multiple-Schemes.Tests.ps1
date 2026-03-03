param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 8.x Authentication (Multiple-Schemes)' -Tag 'Tutorial', 'Auth', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '8.8-Multiple-Schemes.ps1'
        $script:basic = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes('admin:password'))
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /secure/basic returns 401 without credentials' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/basic" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 401
    }

    It 'GET /secure/basic returns 200 with Basic credentials' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/basic" -SkipCertificateCheck -SkipHttpErrorCheck -Headers @{ Authorization = $script:basic }
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Be 'Basic OK'
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }

    It 'GET /secure/key returns 200 with API key' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key" -SkipCertificateCheck -SkipHttpErrorCheck -Headers @{ 'X-Api-Key' = 'my-secret-api-key' }
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Be 'Key OK'
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }

    It 'GET /secure/key returns 200 with Basic credentials (fallback scheme)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/key" -SkipCertificateCheck -SkipHttpErrorCheck -Headers @{ Authorization = $script:basic }
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Be 'Key OK'
    }

    It 'GET /secure/token/new issues a JWT token with Basic credentials' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/token/new" -SkipCertificateCheck -SkipHttpErrorCheck -Headers @{ Authorization = $script:basic }
        $result.StatusCode | Should -Be 200
        $result.Headers.'Content-Type' | Should -Be 'application/json; charset=utf-8'

        $json = $result.Content | ConvertFrom-Json
        $json.access_token | Should -Not -BeNullOrEmpty
        $script:token = $json.access_token
    }

    It 'GET /secure/jwt returns 200 with Bearer token' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/jwt" -SkipCertificateCheck -SkipHttpErrorCheck -Headers @{ Authorization = "Bearer $($script:token)" }
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Be 'JWT OK'
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }
}
