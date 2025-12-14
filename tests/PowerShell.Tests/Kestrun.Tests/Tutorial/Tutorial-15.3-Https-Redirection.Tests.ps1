param()
Describe 'Example 15.3-Https-Redirection' -Tag 'Tutorial', 'Middleware', 'Https' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '15.3-Https-Redirection.ps1' }
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

    It 'Serves content over HTTPS after redirect' {
        $httpsPort = $script:instance.Port + 443
        $uri = "https://$($script:instance.Host):$httpsPort/"

        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 15 -SkipCertificateCheck
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Match 'hello https'
    }
}

