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
}
