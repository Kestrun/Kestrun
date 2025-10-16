param()

Describe 'Example 10.6-Host-Filtering' -Tag 'Tutorial', 'Middleware', 'HostFiltering' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '10.6-HostFiltering.ps1'
    }
    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'Allows allowed host example.com (200)' {
        $base = $script:instance.Url
        $probe = Get-HttpHeadersRaw -Uri "$base/hello" -AsHashtable -HostOverride 'example.com'
        $probe.StatusCode | Should -Be 200
    }

    It 'Allows allowed host www.example.com (200)' {
        $base = $script:instance.Url
        $probe = Get-HttpHeadersRaw -Uri "$base/hello" -AsHashtable -HostOverride 'www.example.com'
        $probe.StatusCode | Should -Be 200
    }

    It 'Blocks disallowed host (400)' {
        $base = $script:instance.Url
        $probe = Get-HttpHeadersRaw -Uri "$base/hello" -AsHashtable -HostOverride 'blocked.example'
        $probe.StatusCode | Should -Be 400
    }

    It 'Rejects empty Host header (400)' {
        $base = $script:instance.Url
        # Force an empty Host header; helper will emit "Host: "
        $probe = Get-HttpHeadersRaw -Uri "$base/hello" -AsHashtable -HostOverride ''
        $probe.StatusCode | Should -Be 400
    }
}
