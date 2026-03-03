param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 6.1-Cert-SelfSigned' -Tag 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '6.1-Cert-SelfSigned.ps1'
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'GET /hello returns hello https over self-signed TLS endpoint' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/hello" -SkipCertificateCheck -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Be 'hello https'
        $result.Headers.'Content-Type' | Should -Be 'text/plain; charset=utf-8'
    }
}
