param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 7.3-Https' {
    m BeforeAll { $script:instance = Start-ExampleScript -Name '7.3-Https.ps1' -PortCount 2 }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }


    It 'GET /unsecure returns hello on primary listener (HTTP)' {
        $uri = "http://127.0.0.1:$($script:instance.Port)/unsecure"
        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Be 'Unsecure hello'
    }

    It 'GET /secure returns hello on secondary listener (HTTPS)' {
        $uri = "https://127.0.0.1:$($script:instance.Port+1)/secure"

        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop -SkipCertificateCheck
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Be 'Secure hello'
    }

    It 'GET /secure fails on primary listener (HTTP)' {
        $uri = "http://127.0.0.1:$($script:instance.Port)/secure"
        { Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop } | Should -Throw
    }

    It 'GET /unsecure fails on secondary listener (HTTPS)' {
        $uri = "https://127.0.0.1:$($script:instance.Port+1)/unsecure"
        { Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop -SkipCertificateCheck } | Should -Throw
    }
}
