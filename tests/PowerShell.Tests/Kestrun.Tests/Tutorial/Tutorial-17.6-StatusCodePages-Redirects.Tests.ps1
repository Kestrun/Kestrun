param()
Describe 'Tutorial 17.6-StatusCodePages-Redirects' -Tag 'Tutorial' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '17.6-StatusCodePages-Redirects.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Hello route returns HTML welcome' {
        $r = Invoke-ExampleRequest -Uri "$($script:instance.Url)/hello" -ReturnRaw
        $r.StatusCode | Should -Be 200
        ($r.Headers['Content-Type'] -join ';') | Should -Match 'text/html'
    }

    It '404 route redirects to /error/404 then returns page' {
        # First request should be a 302/3xx; PowerShell IWR follows redirects by default
        # So assert final content includes the templated error page bits
        $base = $script:instance.Url
        $resp = Invoke-WebRequest -Uri "$base/notfound" -UseBasicParsing -TimeoutSec 12
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Match '<!DOCTYPE html>'
        $resp.Content | Should -Match '404'
    }

    It 'Unmapped route redirects to /error/404 and returns page' {
        $base = $script:instance.Url
        $resp = Invoke-WebRequest -Uri "$base/missing" -UseBasicParsing -TimeoutSec 12
        $resp.StatusCode | Should -Be 200
        $resp.Content | Should -Match '<!DOCTYPE html>'
        $resp.Content | Should -Match '404'
    }
}
