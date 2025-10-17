param()
Describe 'Example 10.7-Forwarded-Header' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '10.7-Forwarded-Header.ps1'
    }
    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'GET /forward reflects forwarded headers' {
        $uri = "$($script:instance.Url)/forward"
        # Simulate reverse proxy by providing X-Forwarded-* headers
        $headers = @{
            'X-Forwarded-For' = '203.0.113.9'              # test public IP (RFC 5737)
            'X-Forwarded-Proto' = 'https'
            'X-Forwarded-Host' = 'proxy.example.test'
        }

        $resp = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 8 -Method Get -Headers $headers -ErrorAction Stop
        $resp.StatusCode | Should -Be 200
        Write-Host "Body: $($resp.Content)" -ForegroundColor Cyan
        $obj = $resp.Content | ConvertFrom-Json

        # Forwarded headers should be applied by middleware
        $obj.scheme | Should -Be 'http'
        $obj.host | Should -Be 'proxy.example.test'
        # RemoteIp should reflect X-Forwarded-For first value
        $obj.remoteIp | Should -Be '203.0.113.9'
    }
}
