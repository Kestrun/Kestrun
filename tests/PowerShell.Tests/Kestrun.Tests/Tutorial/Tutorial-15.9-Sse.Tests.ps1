param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Tutorial 15.9 - SSE (PowerShell)' -Tag 'Tutorial' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '15.9-Sse.ps1'
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'Home page is served' {
        Assert-RouteContent -Uri "$($script:instance.Url)/" -Contains 'SSE Demo' | Out-Null
    }

    It 'SSE endpoint returns text/event-stream and emits events' {
        $uri = "$($script:instance.Url)/sse?count=2&intervalMs=10"
        $invokeParams = @{ Uri = $uri; Method = 'Get'; UseBasicParsing = $true; TimeoutSec = 15; Headers = @{ Accept = 'text/event-stream' } }
        if ($script:instance.Https) { $invokeParams.SkipCertificateCheck = $true }

        $resp = Invoke-WebRequest @invokeParams
        $resp.StatusCode | Should -Be 200
        ($resp.Headers['Content-Type'] -join '') | Should -Match 'text/event-stream'

        $resp.Content | Should -Match 'event:\s*connected'
        $resp.Content | Should -Match 'event:\s*tick'
        $resp.Content | Should -Match 'event:\s*complete'
    }

    It 'SSE endpoint rejects POST' {
        $uri = "$($script:instance.Url)/sse"
        $invokeParams = @{ Uri = $uri; Method = 'Post'; UseBasicParsing = $true; TimeoutSec = 10; SkipHttpErrorCheck = $true }
        if ($script:instance.Https) { $invokeParams.SkipCertificateCheck = $true }

        $resp = Invoke-WebRequest @invokeParams
        $resp.StatusCode | Should -BeGreaterOrEqual 400
        $resp.StatusCode | Should -BeLessThan 500
    }

    It 'Unknown route returns 404' {
        $uri = "$($script:instance.Url)/sse/unknown"
        $invokeParams = @{ Uri = $uri; Method = 'Get'; UseBasicParsing = $true; TimeoutSec = 10; SkipHttpErrorCheck = $true }
        if ($script:instance.Https) { $invokeParams.SkipCertificateCheck = $true }

        $resp = Invoke-WebRequest @invokeParams
        $resp.StatusCode | Should -Be 404
    }

    It 'SSE endpoint rejects PUT' {
        $uri = "$($script:instance.Url)/sse"
        $invokeParams = @{ Uri = $uri; Method = 'Put'; UseBasicParsing = $true; TimeoutSec = 10; SkipHttpErrorCheck = $true }
        if ($script:instance.Https) { $invokeParams.SkipCertificateCheck = $true }

        $resp = Invoke-WebRequest @invokeParams
        $resp.StatusCode | Should -BeGreaterOrEqual 400
        $resp.StatusCode | Should -BeLessThan 500
    }

    It 'SSE endpoint falls back for invalid intervalMs' {
        $uri = "$($script:instance.Url)/sse?count=1&intervalMs=0"
        $invokeParams = @{ Uri = $uri; Method = 'Get'; UseBasicParsing = $true; TimeoutSec = 15; Headers = @{ Accept = 'text/event-stream' } }
        if ($script:instance.Https) { $invokeParams.SkipCertificateCheck = $true }

        $resp = Invoke-WebRequest @invokeParams
        $resp.StatusCode | Should -Be 200
        ($resp.Headers['Content-Type'] -join '') | Should -Match 'text/event-stream'
        $resp.Content | Should -Match 'event:\s*connected'
        $resp.Content | Should -Match 'event:\s*tick'
        $resp.Content | Should -Match 'event:\s*complete'
    }
}
