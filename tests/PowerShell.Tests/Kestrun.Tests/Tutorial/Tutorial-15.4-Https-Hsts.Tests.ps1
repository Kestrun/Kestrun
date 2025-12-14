param()
Describe 'Example 15.4-Https-Hsts' -Tag 'Tutorial', 'Middleware', 'Https', 'Hsts' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '15.4-Https-Hsts.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Redirects HTTP to HTTPS with expected status code' {
        $uri = "http://$($script:instance.Host):$($script:instance.Port)/"
        $probe = Get-HttpHeadersRaw -Uri $uri -Insecure -AsHashtable
        $probe.StatusCode | Should -Be 301
        $probe.Location | Should -Be "https://$($script:instance.Host):$($script:instance.Port + 443)/"
    }

    It 'Serves content over HTTPS after HTTP redirect' {
        $uri = "http://$($script:instance.Host):$($script:instance.Port)/"
        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 15 -SkipCertificateCheck
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Match 'hello https'
    }

    It 'Serves content over HTTPS directly' {
        $httpsPort = $script:instance.Port + 443
        $uri = "https://$($script:instance.Host):$httpsPort/"

        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 15 -SkipCertificateCheck
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Match 'hello https'
    }

    It 'Includes HSTS header in HTTPS response' {
        $httpsPort = $script:instance.Port + 443
        $uri = "https://$($script:instance.Host):$httpsPort/"

        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 15 -SkipCertificateCheck
        $resp.Headers['Strict-Transport-Security'] | Should -Not -BeNullOrEmpty
    }

    It 'HSTS header contains expected directives' {
        $httpsPort = $script:instance.Port + 443
        $uri = "https://$($script:instance.Host):$httpsPort/"

        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 15 -SkipCertificateCheck
        $hstsHeader = $resp.Headers['Strict-Transport-Security']

        # Should contain max-age directive
        $hstsHeader | Should -Match 'max-age=\d+'

        # Should contain includeSubDomains directive (from sample configuration)
        $hstsHeader | Should -Match 'includeSubdomains'

        # Should contain preload directive (from sample configuration)
        $hstsHeader | Should -Match 'preload'
    }

    It 'HSTS header has correct max-age value' {
        $httpsPort = $script:instance.Port + 443
        $uri = "https://$($script:instance.Host):$httpsPort/"

        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 15 -SkipCertificateCheck
        $hstsHeader = $resp.Headers['Strict-Transport-Security']

        # Sample sets 30 days = 30 * 24 * 60 * 60 = 2592000 seconds
        $hstsHeader | Should -Match 'max-age=2592000'
    }

    It 'Does not include HSTS header in HTTP response' {
        # Test HTTP endpoint directly - this should redirect (301) without HSTS header
        $uri = "http://$($script:instance.Host):$($script:instance.Port)/"

        $probe = Get-HttpHeadersRaw -Uri $uri -Insecure -AsHashtable
        # HTTP response should be a redirect (301) and should not have HSTS header
        $probe.StatusCode | Should -Be 301
        $probe.Keys -contains 'Strict-Transport-Security' | Should -Be $false
    }

    It 'Uses self-signed certificate for HTTPS' {
        $httpsPort = $script:instance.Port + 443
        $uri = "https://$($script:instance.Host):$httpsPort/"

        # This should work with -SkipCertificateCheck but fail without it
        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 15 -SkipCertificateCheck
        $resp.StatusCode | Should -Be 200

        # Without -SkipCertificateCheck, should fail due to self-signed cert
        { Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 } | Should -Throw
    }
}

