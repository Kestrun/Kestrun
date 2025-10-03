param()
Describe 'Example 10.3-Https-Redirection' -Tag 'Tutorial', 'Middleware', 'Https' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '10.3-Https-Redirection.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Redirects HTTP to HTTPS with expected status code' {
        $httpUrl = "http://localhost:$($script:instance.Port)/"
        try {
            $resp = Invoke-WebRequest -Uri $httpUrl -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 10 -ErrorAction Stop
            # If no exception, then no redirect occurred (unexpected)
            $false | Should -BeTrue -Because 'Expected a redirect status but got 200-level response'
        } catch {
            $ex = $_.Exception
            if ($ex.Response) {
                $status = $ex.Response.StatusCode.value__
                ($status -in 301, 302, 307, 308) | Should -BeTrue -Because "Expected HTTP redirect status, got $status"
                $location = $ex.Response.Headers['Location']
                $location | Should -Match '^https://'
            } else { throw }
        }
    }

    It 'Serves content over HTTPS after redirect' {
        # We expect instance.Https to be true because the script sets an HTTPS endpoint
        $httpsUrl = "https://localhost:$($script:instance.Port)/" # Start-ExampleScript picks single base port; script adds (+443) for HTTPS
        # If the chosen free port is P, the HTTPS endpoint in script uses P+443. Need to compute that.
        $httpsPort = $script:instance.Port + 443
        $httpsUrl = "https://localhost:$httpsPort/"
        $resp = Invoke-WebRequest -Uri $httpsUrl -UseBasicParsing -TimeoutSec 15 -SkipCertificateCheck
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Match 'hello https'
    }
}
