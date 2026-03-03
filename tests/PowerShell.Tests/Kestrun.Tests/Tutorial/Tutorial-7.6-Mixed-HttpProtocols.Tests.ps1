param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 7.6-Mixed-HttpProtocols' -Tag 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '7.6-Mixed-HttpProtocols.ps1'
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /version responds on HTTP/1.1 listener' {
        $uri = "https://$($script:instance.Host):$($script:instance.Port)/version"
        $resp = Invoke-WebRequest -Uri $uri -HttpVersion 1.1 -SkipCertificateCheck -UseBasicParsing -TimeoutSec 8 -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Match '^Hello via HTTP/'
    }

    It 'GET /version responds on HTTP/2 listener' {
        $uri = "https://$($script:instance.Host):$($script:instance.Port + 1)/version"
        $resp = Invoke-WebRequest -Uri $uri -HttpVersion 2.0 -SkipCertificateCheck -UseBasicParsing -TimeoutSec 8 -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Match '^Hello via HTTP/'
    }

    It 'GET /version responds on mixed HTTP/1.1+HTTP/2 listener' {
        $uri = "https://$($script:instance.Host):$($script:instance.Port + 3)/version"
        $resp = Invoke-WebRequest -Uri $uri -SkipCertificateCheck -UseBasicParsing -TimeoutSec 8 -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        ($resp.Content.Trim()) | Should -Match '^Hello via HTTP/'
    }
}
