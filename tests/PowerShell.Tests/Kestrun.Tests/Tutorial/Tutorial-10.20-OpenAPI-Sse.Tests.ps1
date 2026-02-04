param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Tutorial 10.20 - OpenAPI SSE (PowerShell)' -Tag 'Tutorial' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.20-OpenAPI-Sse.ps1'
    }

    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'OpenAPI JSON is served and documents the SSE endpoint' {
        $openApi = "$($script:instance.Url)/openapi/v3.1/openapi.json"
        Assert-RouteContent -Uri $openApi -Contains '/sse' | Out-Null
        Assert-RouteContent -Uri $openApi -Contains 'text/event-stream' | Out-Null
    }

    It 'SSE endpoint returns text/event-stream and emits events' {
        $uri = "$($script:instance.Url)/sse?count=2&intervalMs=10"
        $invokeParams = @{ Uri = $uri; Method = 'Get'; UseBasicParsing = $true; TimeoutSec = 15; Headers = @{ Accept = 'text/event-stream' } }
        if ($script:instance.Https) { $invokeParams.SkipCertificateCheck = $true }

        $resp = Invoke-WebRequest @invokeParams
        $resp.StatusCode | Should -Be 200
        ($resp.Headers['Content-Type'] -join '') | Should -Match 'text/event-stream'
        $resp.Content | Should -Match 'event:\s*connected'
    }
}
