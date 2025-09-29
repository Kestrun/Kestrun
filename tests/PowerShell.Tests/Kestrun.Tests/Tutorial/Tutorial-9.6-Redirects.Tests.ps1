param()
Describe 'Example 9.6-Redirects' -Tag 'Tutorial', 'Redirects', 'Slow' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '9.6-Redirects.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Route /old issues redirect with expected Location header' {
        $url = "http://127.0.0.1:$($script:instance.Port)/old"
        $resp = $null
        try {
            # Prevent auto-follow to external site
            $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop
        } catch {
            if ($_.Exception.Response) { $resp = $_.Exception.Response } else { throw }
        }
        # Normalize status code
        if ($resp -is [System.Net.Http.HttpResponseMessage]) {
            $status = [int]$resp.StatusCode
        } else {
            $status = [int]$resp.StatusCode
        }
        $status | Should -BeIn 200, 301, 302, 307, 308
        # Extract Location header for both object shapes
        $loc = $null
        if ($resp -is [Microsoft.PowerShell.Commands.BasicHtmlWebResponseObject]) {
            $loc = $resp.Headers['Location']
            if ($loc -is [Array]) { $loc = $loc[0] }
        } elseif ($resp -is [System.Net.Http.HttpResponseMessage]) {
            if ($resp.Headers.Location) { $loc = $resp.Headers.Location.AbsoluteUri }
        }
        $loc | Should -Be 'https://example.com/new'
        if ($status -eq 200 -and $resp -is [Microsoft.PowerShell.Commands.BasicHtmlWebResponseObject]) {
            ($resp.Content -like '*Resource moved*') | Should -BeTrue
        }
    }
}
