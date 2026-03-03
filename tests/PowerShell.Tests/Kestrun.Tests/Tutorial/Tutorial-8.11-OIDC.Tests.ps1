param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

function Test-DuendeAuthorityAvailable {
    [CmdletBinding()]
    param()

    try {
        $probe = Invoke-WebRequest -Uri 'https://demo.duendesoftware.com/.well-known/openid-configuration' -SkipHttpErrorCheck -TimeoutSec 8
        return ($probe.StatusCode -eq 200)
    } catch {
        return $false
    }
}

$script:skipOidc = -not (Test-DuendeAuthorityAvailable)

Describe 'Example 8.11 Authentication (OpenID Connect - Duende Demo)' -Tag 'Tutorial', 'Auth', 'Slow' -Skip:$script:skipOidc {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '8.11-OIDC.ps1'
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET / returns OIDC landing page' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/" -SkipCertificateCheck -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Headers.'Content-Type' | Should -Be 'text/html; charset=utf-8'
        $result.Content | Should -Match 'OIDC|OpenID|Duende|Login|Sign\s*In'
    }

    It 'GET /login challenges unauthenticated users' {
        $result = Get-HttpHeadersRaw -Uri "$($script:instance.Url)/login" -Insecure -AsHashtable
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -BeIn @(302, 401)
    }

    It 'GET /me challenges unauthenticated users' {
        $result = Get-HttpHeadersRaw -Uri "$($script:instance.Url)/me" -Insecure -AsHashtable
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -BeIn @(302, 401)
    }
}
