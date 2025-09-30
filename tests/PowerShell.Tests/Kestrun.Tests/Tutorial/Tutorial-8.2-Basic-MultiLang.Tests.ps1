param()
. "./tests/PowerShell.Tests/Kestrun.Tests/PesterHelpers.ps1"
Describe 'Example 8.x Authentication (Basic-MultiLang)' -Tag 'Tutorial', 'Slow' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '8.2-Basic-MultiLang.ps1'
        # Precompute Basic auth header value for tests
        # Username: admin, Password: password
        $creds = 'admin:password'
        $script:basic = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($creds)) }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }


    It 'cs/hello in CSharp' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/cs/hello" -SkipCertificateCheck -Headers @{Authorization = $script:basic }
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Be 'CS Hello, admin!'
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }

    It 'vb/hello in VBNet' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/secure/vb/hello" -SkipCertificateCheck -Headers @{Authorization = $script:basic }
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Be 'VB Hello, admin!'
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }
}
