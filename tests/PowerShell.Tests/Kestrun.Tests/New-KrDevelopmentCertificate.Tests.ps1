[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'New-KrDevelopmentCertificate' {
    BeforeEach {
        $script:bundle = $null
        $script:reusedRoot = $null
    }

    AfterEach {
        if ($script:bundle) {
            if ($script:bundle.LeafCertificate) {
                $script:bundle.LeafCertificate.Dispose()
            }

            if ($script:bundle.RootCertificate) {
                $script:bundle.RootCertificate.Dispose()
            }
        }

        if ($script:reusedRoot) {
            $script:reusedRoot.Dispose()
        }

        $script:bundle = $null
        $script:reusedRoot = $null
    }

    It 'supports should-process semantics for root trust changes' {
        $command = Get-Command New-KrDevelopmentCertificate

        $command.Parameters.ContainsKey('WhatIf') | Should -BeTrue
        $command.Parameters.ContainsKey('Confirm') | Should -BeTrue
    }

    It 'fails early with a clear error when TrustRoot is requested on non-Windows' -Skip:$IsWindows {
        {
            New-KrDevelopmentCertificate -TrustRoot -Exportable
        } | Should -Throw -ExpectedMessage '*-TrustRoot parameter is only supported on Windows*trust the root certificate manually*'
    }

    It 'creates a default development root and localhost leaf bundle' {
        $script:bundle = New-KrDevelopmentCertificate -Exportable

        $script:bundle | Should -Not -BeNullOrEmpty
        $script:bundle.RootTrusted | Should -BeFalse
        $script:bundle.RootCertificate | Should -Not -BeNullOrEmpty
        $script:bundle.LeafCertificate | Should -Not -BeNullOrEmpty

        $script:bundle.RootCertificate.Subject | Should -Be 'CN=Kestrun Development Root CA'
        $script:bundle.LeafCertificate.Subject | Should -Be 'CN=localhost'
        $script:bundle.LeafCertificate.Issuer | Should -Be $script:bundle.RootCertificate.Subject
        $script:bundle.RootCertificate.HasPrivateKey | Should -BeTrue
        $script:bundle.LeafCertificate.HasPrivateKey | Should -BeTrue
    }

    It 'reuses a supplied root certificate when issuing the leaf certificate' {
        $script:reusedRoot = New-KrSelfSignedCertificate `
            -DnsNames 'Reusable Development Root CA' `
            -CertificateAuthority `
            -Exportable

        $script:bundle = New-KrDevelopmentCertificate `
            -RootCertificate $script:reusedRoot `
            -DnsNames 'localhost', 'dev.localtest.me' `
            -Exportable

        [object]::ReferenceEquals($script:reusedRoot, $script:bundle.RootCertificate) | Should -BeTrue
        $script:bundle.RootCertificate.Subject | Should -Be 'CN=Reusable Development Root CA'
        $script:bundle.LeafCertificate.Issuer | Should -Be $script:reusedRoot.Subject
        $script:bundle.LeafCertificate.Subject | Should -Be 'CN=localhost'
        $script:bundle.RootTrusted | Should -BeFalse
    }

    It 'skips trusting the root certificate when WhatIf is used' {
        $script:bundle = New-KrDevelopmentCertificate -TrustRoot -Exportable -WhatIf

        $script:bundle | Should -Not -BeNullOrEmpty
        $script:bundle.RootCertificate | Should -Not -BeNullOrEmpty
        $script:bundle.LeafCertificate | Should -Not -BeNullOrEmpty
        $script:bundle.RootTrusted | Should -BeFalse
    }
}
