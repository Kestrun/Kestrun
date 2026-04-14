[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
    Import-Module (Get-KestrunModulePath) -Force
}

Describe 'New-KrSelfSignedCertificate' {
    BeforeEach {
        $script:certificate = $null
    }

    AfterEach {
        if ($script:certificate) {
            $script:certificate.Dispose()
        }

        $script:certificate = $null
    }

    It 'accepts PowerShell-friendly key usage arrays and combines them for the generated certificate' {
        $script:certificate = New-KrSelfSignedCertificate -DnsNames 'localhost' -KeyUsage DigitalSignature, KeyEncipherment -Exportable

        $script:certificate | Should -Not -BeNullOrEmpty

        $keyUsageExtension = $script:certificate.Extensions |
            Where-Object { $_.Oid.Value -eq '2.5.29.15' } |
            Select-Object -First 1

        $keyUsageExtension | Should -Not -BeNullOrEmpty
        $keyUsageExtension = [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]$keyUsageExtension

        $expected = [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature -bor [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyEncipherment
        $keyUsageExtension.KeyUsages | Should -Be $expected
    }
}
