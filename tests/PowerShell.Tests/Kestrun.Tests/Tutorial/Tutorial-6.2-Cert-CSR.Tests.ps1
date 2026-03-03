param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 6.2-Cert-CSR' -Tag 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '6.2-Cert-CSR.ps1'
    }

    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'POST /certs/csr returns PEM payload for valid request' {
        $body = @{
            DnsNames = @('example.com', 'www.example.com')
            KeyType = 'Rsa'
            KeyLength = 2048
            Country = 'US'
            Org = 'Acme Ltd.'
            CommonName = 'example.com'
        } | ConvertTo-Json

        $result = Invoke-WebRequest -Method Post -Uri "$($script:instance.Url)/certs/csr" -Body $body -ContentType 'application/json' -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 200
        $result.Headers.'Content-Type' | Should -Be 'application/json; charset=utf-8'

        $json = $result.Content | ConvertFrom-Json
        $json.csrPem | Should -Match '-----BEGIN CERTIFICATE REQUEST-----'
        $json.privateKeyPem | Should -Match '-----BEGIN '
        $json.publicKeyPem | Should -Match '-----BEGIN PUBLIC KEY-----'
    }

    It 'POST /certs/csr returns 400 for invalid key parameters' {
        $body = @{
            DnsNames = @('example.com')
            KeyType = 'InvalidKeyType'
            KeyLength = 2048
            CommonName = 'example.com'
        } | ConvertTo-Json

        $result = Invoke-WebRequest -Method Post -Uri "$($script:instance.Url)/certs/csr" -Body $body -ContentType 'application/json' -SkipHttpErrorCheck
        $result | Should -Not -BeNullOrEmpty
        $result.StatusCode | Should -Be 400
        $result.Headers.'Content-Type' | Should -Be 'application/json; charset=utf-8'

        $json = $result.Content | ConvertFrom-Json
        $json.error | Should -Not -BeNullOrEmpty
    }
}
