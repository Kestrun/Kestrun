[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
    Import-Module (Get-KestrunModulePath) -Force
}

Describe 'New-KrSelfSignedCertificate' {
    BeforeEach {
        $script:certificate = $null
        $script:bundle = $null
        $script:reusedRoot = $null
    }

    AfterEach {
        if ($script:certificate) {
            $script:certificate.Dispose()
        }

        if ($script:bundle) {
            if ($script:bundle.LeafCertificate) {
                $script:bundle.LeafCertificate.Dispose()
            }

            if ($script:bundle.PublicRootCertificate) {
                $script:bundle.PublicRootCertificate.Dispose()
            }

            if ($script:bundle.RootCertificate) {
                $script:bundle.RootCertificate.Dispose()
            }
        }

        if ($script:reusedRoot) {
            $script:reusedRoot.Dispose()
        }

        $script:certificate = $null
        $script:bundle = $null
        $script:reusedRoot = $null
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

    It 'supports should-process semantics for root trust changes in development mode' {
        $command = Get-Command New-KrSelfSignedCertificate

        $command.Parameters.ContainsKey('WhatIf') | Should -BeTrue
        $command.Parameters.ContainsKey('Confirm') | Should -BeTrue
    }

    It 'fails early with a clear error when TrustRoot is requested on non-Windows in development mode' -Skip:$IsWindows {
        {
            New-KrSelfSignedCertificate -Development -TrustRoot -Exportable
        } | Should -Throw -ExpectedMessage '*-TrustRoot parameter is only supported on Windows*trust the root certificate manually*'
    }

    It 'creates a default development root and localhost leaf bundle' {
        $script:bundle = New-KrSelfSignedCertificate -Development -Exportable

        $script:bundle | Should -Not -BeNullOrEmpty
        $script:bundle.RootTrusted | Should -BeFalse
        $script:bundle.RootCertificate | Should -Not -BeNullOrEmpty
        $script:bundle.LeafCertificate | Should -Not -BeNullOrEmpty

        $script:bundle.RootCertificate.Subject | Should -Be 'CN=Kestrun Development Root CA'
        $script:bundle.LeafCertificate.Subject | Should -Be 'CN=localhost'
        $script:bundle.LeafCertificate.Issuer | Should -Be $script:bundle.RootCertificate.Subject
        $script:bundle.RootCertificate.HasPrivateKey | Should -BeTrue
        $script:bundle.PublicRootCertificate | Should -Not -BeNullOrEmpty
        $script:bundle.PublicRootCertificate.HasPrivateKey | Should -BeFalse
        $script:bundle.PublicRootCertificate.Thumbprint | Should -Be $script:bundle.RootCertificate.Thumbprint
        $script:bundle.LeafCertificate.HasPrivateKey | Should -BeTrue
    }

    It 'reuses a supplied root certificate when issuing the development leaf certificate' {
        $script:reusedRoot = New-KrSelfSignedCertificate `
            -DnsNames 'Reusable Development Root CA' `
            -CertificateAuthority `
            -Exportable

        $script:bundle = New-KrSelfSignedCertificate `
            -Development `
            -RootCertificate $script:reusedRoot `
            -DnsNames 'localhost', 'dev.localtest.me' `
            -Exportable

        [object]::ReferenceEquals($script:reusedRoot, $script:bundle.RootCertificate) | Should -BeTrue
        $script:bundle.RootCertificate.Subject | Should -Be 'CN=Reusable Development Root CA'
        $script:bundle.LeafCertificate.Issuer | Should -Be $script:reusedRoot.Subject
        $script:bundle.LeafCertificate.Subject | Should -Be 'CN=localhost'
        $script:bundle.PublicRootCertificate | Should -Not -BeNullOrEmpty
        $script:bundle.PublicRootCertificate.HasPrivateKey | Should -BeFalse
        $script:bundle.PublicRootCertificate.Thumbprint | Should -Be $script:reusedRoot.Thumbprint
        $script:bundle.RootTrusted | Should -BeFalse
    }

    It 'skips trusting the development root certificate when WhatIf is used' -Skip:(!$IsWindows) {
        $script:bundle = New-KrSelfSignedCertificate -Development -TrustRoot -Exportable -WhatIf

        $script:bundle | Should -Not -BeNullOrEmpty
        $script:bundle.RootCertificate | Should -Not -BeNullOrEmpty
        $script:bundle.LeafCertificate | Should -Not -BeNullOrEmpty
        $script:bundle.RootTrusted | Should -BeFalse
    }
}
