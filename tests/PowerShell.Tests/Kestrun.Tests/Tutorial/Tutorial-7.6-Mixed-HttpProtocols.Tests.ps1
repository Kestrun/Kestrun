param()

BeforeDiscovery {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
    $script:supportsHttp3 = Test-KrCapability -Feature 'Http3'
}

BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
    if (-not (Get-Variable -Name supportsHttp3 -Scope Script -ErrorAction SilentlyContinue)) {
        $script:supportsHttp3 = Test-KrCapability -Feature 'Http3'
    }
}

Describe 'Example 7.6-Mixed-HttpProtocols' -Tag 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '7.6-Mixed-HttpProtocols.ps1' -StartupTimeoutSeconds 80
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /version responds on HTTP/1.1 listener' {
        $uri = "https://$($script:instance.Host):$($script:instance.Port)/version"
        $resp = Invoke-TestRequest -Uri $uri -HttpVersion 1.1 -SkipCertificateCheck -UseBasicParsing -TimeoutSec 8 -ErrorAction Stop

        $resp.StatusCode | Should -Be 200
        $resp.BaseResponse.Version | Should -Be '1.1'
        ($resp.Content.Trim()) | Should -Be 'Hello via HTTP/1.1'
    }

    It 'GET /version responds on HTTP/2 listener' {
        $uri = "https://$($script:instance.Host):$($script:instance.Port + 1)/version"
        $resp = Invoke-TestRequest -Uri $uri -HttpVersion 2.0 -SkipCertificateCheck -UseBasicParsing -TimeoutSec 8 -ErrorAction Stop

        $resp.StatusCode | Should -Be 200
        $resp.BaseResponse.Version | Should -Be '2.0'
        ($resp.Content.Trim()) | Should -Be 'Hello via HTTP/2'
    }

    It 'GET /version responds on HTTP/3 listener' -Skip:(-not $script:supportsHttp3) {
        $uri = "https://127.0.0.1:$($script:instance.Port + 2)/version"
        $handler = [System.Net.Http.HttpClientHandler]::new()
        $handler.ServerCertificateCustomValidationCallback = [System.Net.Http.HttpClientHandler]::DangerousAcceptAnyServerCertificateValidator
        $client = [System.Net.Http.HttpClient]::new($handler)
        $client.Timeout = [TimeSpan]::FromSeconds(8)
        $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $uri)
        $request.Version = [Version]::new(3, 0)
        $request.VersionPolicy = [System.Net.Http.HttpVersionPolicy]::RequestVersionExact
        $response = $null

        try {
            $response = $client.SendAsync($request).GetAwaiter().GetResult()
            $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

            [int]$response.StatusCode | Should -Be 200
            $response.Version.ToString() | Should -Be '3.0'
            $content.Trim() | Should -Be 'Hello via HTTP/3'
        } finally {
            if ($response) { $response.Dispose() }
            $request.Dispose()
            $client.Dispose()
            $handler.Dispose()
        }
    }

    Describe 'Combined HTTP/1.1+HTTP/2 listener' {
        It 'GET /version responds on HTTP/1.1 request' {
            $uri = "https://$($script:instance.Host):$($script:instance.Port + 3)/version"
            $resp = Invoke-TestRequest -Uri $uri -HttpVersion 1.1 -SkipCertificateCheck -UseBasicParsing -TimeoutSec 8 -ErrorAction Stop

            $resp.StatusCode | Should -Be 200
            $resp.BaseResponse.Version | Should -Be '1.1'
            ($resp.Content.Trim()) | Should -Be 'Hello via HTTP/1.1'
        }

        It 'GET /version responds on HTTP/2.0 request' {
            $uri = "https://$($script:instance.Host):$($script:instance.Port + 3)/version"
            $resp = Invoke-TestRequest -Uri $uri -HttpVersion 2.0 -SkipCertificateCheck -UseBasicParsing -TimeoutSec 8 -ErrorAction Stop

            $resp.StatusCode | Should -Be 200
            $resp.BaseResponse.Version | Should -Be '2.0'
            ($resp.Content.Trim()) | Should -Be 'Hello via HTTP/2'
        }
    }

    Describe 'Combined HTTP/1.1+HTTP/2+HTTP/3 listener' {
        It 'GET /version responds on HTTP/1.1 request' {
            $uri = "https://$($script:instance.Host):$($script:instance.Port + 4)/version"
            $resp = Invoke-TestRequest -Uri $uri -HttpVersion 1.1 -SkipCertificateCheck -UseBasicParsing -TimeoutSec 8 -ErrorAction Stop

            $resp.StatusCode | Should -Be 200
            $resp.BaseResponse.Version | Should -Be '1.1'
            ($resp.Content.Trim()) | Should -Be 'Hello via HTTP/1.1'
        }

        It 'GET /version responds on HTTP/2.0 request' {
            $uri = "https://$($script:instance.Host):$($script:instance.Port + 4)/version"
            $resp = Invoke-TestRequest -Uri $uri -HttpVersion 2.0 -SkipCertificateCheck -UseBasicParsing -TimeoutSec 8 -ErrorAction Stop

            $resp.StatusCode | Should -Be 200
            $resp.BaseResponse.Version | Should -Be '2.0'
            ($resp.Content.Trim()) | Should -Be 'Hello via HTTP/2'
        }

        It 'GET /version responds on HTTP/3.0 request' -Skip:(-not ($script:supportsHttp3 )) {
            $uri = "https://127.0.0.1:$($script:instance.Port + 4)/version"
            $handler = [System.Net.Http.HttpClientHandler]::new()
            $handler.ServerCertificateCustomValidationCallback = [System.Net.Http.HttpClientHandler]::DangerousAcceptAnyServerCertificateValidator
            $client = [System.Net.Http.HttpClient]::new($handler)
            $client.Timeout = [TimeSpan]::FromSeconds(8)
            $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $uri)
            $request.Version = [Version]::new(3, 0)
            $request.VersionPolicy = [System.Net.Http.HttpVersionPolicy]::RequestVersionExact
            $response = $null

            try {
                $response = $client.SendAsync($request).GetAwaiter().GetResult()
                $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

                [int]$response.StatusCode | Should -Be 200
                $response.Version.ToString() | Should -Be '3.0'
                $content.Trim() | Should -Match '^Hello via HTTP/'
            } finally {
                if ($response) { $response.Dispose() }
                $request.Dispose()
                $client.Dispose()
                $handler.Dispose()
            }
        }
    }
}
