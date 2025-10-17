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

        # Simulate reverse proxy
        $headers = @{
            'X-Forwarded-For' = '203.0.113.9'         # RFC 5737 test IP
            'X-Forwarded-Proto' = 'https'
            'X-Forwarded-Host' = 'proxy.example.test'
            # If your app uses PathBase via X-Forwarded-Prefix, include it:
            'X-Forwarded-Prefix' = '/myapp'
        }

        $resp = Invoke-WebRequest -Uri $uri -TimeoutSec 8 -Method Get -Headers $headers -ErrorAction Stop
        $resp.StatusCode | Should -Be 200

        $obj = $resp.Content | ConvertFrom-Json
        if (-not $obj) {
            Write-Host "Raw Body: $($resp.Content)" -ForegroundColor Yellow
            throw 'Response body was not valid JSON.'
        }

        # Forwarded headers should be applied by middleware
        $obj.scheme | Should -Be 'https'                     # because X-Forwarded-Proto: https
        $obj.host | Should -Be 'proxy.example.test'        # because X-Forwarded-Host
        $obj.remoteIp | Should -Be '203.0.113.9'             # first IP from X-Forwarded-For

        # Optional sanity checks if your route returns them:
        $obj.basePath | Should -Be '/myapp'
        $obj.fullPath | Should -Be '/myapp/forward'
    }
}
