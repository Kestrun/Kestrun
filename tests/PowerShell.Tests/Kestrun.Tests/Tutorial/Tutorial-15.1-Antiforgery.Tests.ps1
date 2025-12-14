param()

Describe 'Example 15.1-Antiforgery' -Tag 'Tutorial', 'Middleware', 'Antiforgery' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '15.1-Antiforgery.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Allows safe GET routes without antiforgery token' {
        $base = $script:instance.Url
        Assert-RouteContent -Uri "$base/hello" -Contains 'Hello, World!'
        Assert-RouteContent -Uri "$base/hello-json" -JsonField 'message' -JsonValue 'Hello, World!'
        Assert-RouteContent -Uri "$base/hello-xml" -Regex '<message>\s*Hello, World!\s*</message>'
        Assert-RouteContent -Uri "$base/hello-yaml" -YamlKey 'message' -YamlValue 'Hello, World!'
    }

    It 'Allows marked safe POST routes without antiforgery token' {
        Assert-RouteContent -Uri "$($script:instance.Url)/json" -Method 'Post' -Headers @{ message = 'Ping' } -JsonField 'message' -JsonValue 'Ping'
    }

    It 'Issues antiforgery token and cookie via /csrf-token' {
        $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
        $tokenParams = @{
            Uri = "$($script:instance.Url)/csrf-token"
            UseBasicParsing = $true
            Method = 'Get'
            TimeoutSec = 10
            WebSession = $session
            SkipCertificateCheck = $true
        }
        $tokenResp = Invoke-WebRequest @tokenParams
        $tokenResp.StatusCode | Should -Be 200
        $payload = $tokenResp.Content | ConvertFrom-Json
        $payload.token | Should -Not -BeNullOrEmpty

        $targetUri = [uri]$script:instance.Url
        $cookie = $session.Cookies.GetCookies($targetUri) | Where-Object { $_.Name -like '*.AntiXSRF' }
        $cookie | Should -Not -BeNullOrEmpty -Because 'Antiforgery cookie should be issued alongside token'
    }

    It 'Allows protected POST when token and cookie provided' {
        $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
        $tokenParams = @{
            Uri = "$($script:instance.Url)/csrf-token"
            UseBasicParsing = $true
            Method = 'Get'
            TimeoutSec = 10
            WebSession = $session
            SkipCertificateCheck = $true
        }
        $tokenResp = Invoke-WebRequest @tokenParams
        $token = ($tokenResp.Content | ConvertFrom-Json).token
        $token | Should -Not -BeNullOrEmpty

        $headers = @{ 'X-CSRF-TOKEN' = $token }
        $body = '{"name":"Alice"}'
        $postParams = @{
            Uri = "$($script:instance.Url)/profile"
            UseBasicParsing = $true
            TimeoutSec = 10
            Method = 'Post'
            ContentType = 'application/json'
            Body = $body
            Headers = $headers
            WebSession = $session
            SkipCertificateCheck = $true
        }
        $post = Invoke-WebRequest @postParams
        $post.StatusCode | Should -Be 200
        $result = $post.Content | ConvertFrom-Json
        $result.saved | Should -BeTrue
        $result.name | Should -Be 'Alice'
    }

    It 'Rejects protected POST when token missing' {
        $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
        # Fetch token once to ensure antiforgery cookie is present (but intentionally drop header later)
        $tokenParams = @{
            Uri = "$($script:instance.Url)/csrf-token"
            UseBasicParsing = $true
            Method = 'Get'
            TimeoutSec = 10
            WebSession = $session
            SkipCertificateCheck = $true
        }
        Invoke-WebRequest @tokenParams | Out-Null

        $body = '{"name":"Mallory"}'
        $respParams = @{
            Uri = "$($script:instance.Url)/profile"
            UseBasicParsing = $true
            TimeoutSec = 10
            Method = 'Post'
            ContentType = 'application/json'
            Body = $body
            WebSession = $session
            SkipCertificateCheck = $true
            SkipHttpErrorCheck = $true
        }
        $resp = Invoke-WebRequest @respParams
        ($resp.StatusCode -in 400, 403) | Should -BeTrue -Because 'Missing antiforgery header should be rejected (400 or 403)'
    }
}

