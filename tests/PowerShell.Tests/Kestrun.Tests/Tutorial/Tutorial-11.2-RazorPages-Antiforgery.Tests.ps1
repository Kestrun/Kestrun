param()

Describe 'Example 11.2-RazorPages-Antiforgery' -Tag 'Tutorial' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '11.2-RazorPages-Antiforgery.ps1'

        function New-IwrParams {
            param(
                [Parameter(Mandatory)] [string] $Uri,
                [Parameter(Mandatory)] [string] $Method,
                [Microsoft.PowerShell.Commands.WebRequestSession] $WebSession,
                [hashtable] $Headers,
                [string] $ContentType,
                [object] $Body
            )

            $p = @{ Uri = $Uri; Method = $Method; UseBasicParsing = $true; TimeoutSec = 12 }
            if ($Uri -like 'https://*' -and (Get-Command Invoke-WebRequest).Parameters.ContainsKey('SkipCertificateCheck')) {
                $p.SkipCertificateCheck = $true
            }
            if ($WebSession) { $p.WebSession = $WebSession }
            if ($Headers) { $p.Headers = $Headers }
            if ($ContentType) { $p.ContentType = $ContentType }
            if ($Body) { $p.Body = $Body }
            if ((Get-Command Invoke-WebRequest).Parameters.ContainsKey('SkipHttpErrorCheck')) {
                $p.SkipHttpErrorCheck = $true
            }
            return $p
        }

        function Get-CsrfToken {
            param([Parameter(Mandatory)][Microsoft.PowerShell.Commands.WebRequestSession]$Session)

            $p = New-IwrParams -Uri "$($script:instance.Url)/csrf-token" -Method 'Get' -WebSession $Session
            $resp = Invoke-WebRequest @p
            $resp.StatusCode | Should -Be 200
            $payload = $resp.Content | ConvertFrom-Json -ErrorAction Stop

            $payload.token | Should -Not -BeNullOrEmpty
            # HeaderName may be null if antiforgery options weren't configured; in this sample it should be set
            ($payload.headerName ?? 'X-CSRF-TOKEN') | Should -Be 'X-CSRF-TOKEN'

            return $payload.token
        }
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'Serves the Razor home page (HTTPS)' {
        Assert-RouteContent -Uri "$($script:instance.Url)/" -Contains 'What this demo shows'
    }

    It 'Issues antiforgery cookie + token via /csrf-token' {
        $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
        $null = Get-CsrfToken -Session $session

        # Ensure the antiforgery cookie was set
        $cookieHeader = ($session.Cookies.GetCookies([Uri]$($script:instance.Url)) | Where-Object { $_.Name -eq '.Kestrun.AntiXSRF' })
        $cookieHeader | Should -Not -BeNullOrEmpty
    }

    It 'Rejects protected POST when token header is missing' {
        $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
        $null = Get-CsrfToken -Session $session

        $p = New-IwrParams -Uri "$($script:instance.Url)/api/operation/start?seconds=1" -Method 'Post' -WebSession $session
        $resp = Invoke-WebRequest @p
        ($resp.StatusCode -in 400, 403) | Should -BeTrue -Because 'Missing X-CSRF-TOKEN should be rejected'
    }

    It 'Allows protected POST when cookie + token header are present' {
        $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
        $token = Get-CsrfToken -Session $session

        $headers = @{ 'X-CSRF-TOKEN' = $token }
        $p = New-IwrParams -Uri "$($script:instance.Url)/api/operation/start?seconds=1" -Method 'Post' -WebSession $session -Headers $headers
        $resp = Invoke-WebRequest @p
        $resp.StatusCode | Should -Be 200

        $obj = $resp.Content | ConvertFrom-Json -ErrorAction Stop
        $obj.Success | Should -BeTrue
        $obj.TaskId | Should -Not -BeNullOrEmpty

        # Cancel task (also requires antiforgery)
        $p = New-IwrParams -Uri "$($script:instance.Url)/tasks/cancel?id=$($obj.TaskId)" -Method 'Post' -WebSession $session -Headers $headers
        $cancel = Invoke-WebRequest @p
        $cancel.StatusCode | Should -Be 200
        ($cancel.Content | ConvertFrom-Json -ErrorAction Stop).Success | Should -BeTrue
    }
}
