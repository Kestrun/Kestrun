[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
    Import-Module (Get-KestrunModulePath) -Force

    function Invoke-TestKrCertificateWithReason {
        param(
            [Parameter(Mandatory)]
            [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,

            [Parameter()]
            [System.Security.Cryptography.X509Certificates.X509Certificate2[]]$CertificateChain
        )

        $isValid = Test-KrCertificate -Certificate $Certificate -CertificateChain $CertificateChain -FailureReasonVariable 'reason'
        return [pscustomobject]@{
            IsValid = $isValid
            Reason = $reason
        }
    }
}

Describe 'Test-KrCertificate' {
    BeforeEach {
        $script:bundle = $null
    }

    AfterEach {
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

        $script:bundle = $null
    }

    It 'accepts an explicit root chain for development certificates' {
        $script:bundle = New-KrDevelopmentCertificate -Exportable

        $withoutChain = Invoke-TestKrCertificateWithReason -Certificate $script:bundle.LeafCertificate
        $withoutChain.IsValid | Should -BeFalse
        $withoutChain.Reason | Should -Match 'PartialChain|trusted root authority'

        $withChain = Invoke-TestKrCertificateWithReason -Certificate $script:bundle.LeafCertificate -CertificateChain $script:bundle.RootCertificate
        $withChain.IsValid | Should -BeTrue
        $withChain.Reason | Should -Be ''
    }
}
