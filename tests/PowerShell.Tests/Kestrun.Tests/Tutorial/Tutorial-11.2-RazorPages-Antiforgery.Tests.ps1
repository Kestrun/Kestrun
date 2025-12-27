param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 11.2-RazorPages-Antiforgery' -Tag 'Tutorial' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '11.2-RazorPages-Antiforgery.ps1'
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'Serves the Razor home page (HTTPS)' {
        Assert-RouteContent -Uri "$($script:instance.Url)/" -Contains 'What this demo shows'
    }

    It 'Issues antiforgery cookie + token via /csrf-token' {
        $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
        $null = Get-CsrfToken -InstanceUrl $script:instance.Url -Session $session

        $cookieHeader = ($session.Cookies.GetCookies([Uri]$($script:instance.Url)) |
                Where-Object { $_.Name -eq '.Kestrun.AntiXSRF' })
        $cookieHeader | Should -Not -BeNullOrEmpty
    }

    It 'Rejects protected POST when token header is missing' {
        $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
        $null = Get-CsrfToken -InstanceUrl $script:instance.Url-Session $session

        $p = @{
            Uri = "$($script:instance.Url)/api/operation/start?seconds=1"
            Method = 'Post'
            UseBasicParsing = $true
            TimeoutSec = 12
            WebSession = $session
            SkipHttpErrorCheck = $true
            SkipCertificateCheck = $true
        }

        $resp = Invoke-WebRequest @p
        ($resp.StatusCode -in 400, 403) | Should -BeTrue -Because 'Missing X-CSRF-TOKEN should be rejected'
    }

    It 'Allows protected POST when cookie + token header are present' {
        $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
        $token = Get-CsrfToken -InstanceUrl $script:instance.Url-Session $session

        $headers = @{ 'X-CSRF-TOKEN' = $token }

        $p = @{
            Uri = "$($script:instance.Url)/api/operation/start?seconds=1"
            Method = 'Post'
            UseBasicParsing = $true
            TimeoutSec = 12
            WebSession = $session
            Headers = $headers
            SkipHttpErrorCheck = $true
            SkipCertificateCheck = $true
        }

        $resp = Invoke-WebRequest @p
        $resp.StatusCode | Should -Be 200

        $obj = $resp.Content | ConvertFrom-Json -ErrorAction Stop
        $obj.Success | Should -BeTrue
        $obj.TaskId | Should -Not -BeNullOrEmpty

        $p = @{
            Uri = "$($script:instance.Url)/tasks/cancel?id=$($obj.TaskId)"
            Method = 'Post'
            UseBasicParsing = $true
            TimeoutSec = 12
            WebSession = $session
            Headers = $headers
            SkipHttpErrorCheck = $true
            SkipCertificateCheck = $true
        }

        $cancel = Invoke-WebRequest @p
        $cancel.StatusCode | Should -Be 200
        ($cancel.Content | ConvertFrom-Json -ErrorAction Stop).Success | Should -BeTrue
    }
}
