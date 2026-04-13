[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
    Import-Module (Get-KestrunModulePath) -Force
}

Describe 'New-KrCertificateRequest' {
    It 'accepts PowerShell-friendly key usage arrays and emits the combined CSR key usage extension' {
        $csr = New-KrCertificateRequest -DnsNames 'localhost' -CommonName 'localhost' -KeyUsage DigitalSignature, KeyEncipherment

        $csr | Should -Not -BeNullOrEmpty
        $csr.CsrPem | Should -Match 'BEGIN CERTIFICATE REQUEST'

        $reader = [System.IO.StringReader]::new($csr.CsrPem)
        try {
            $pemReader = [Org.BouncyCastle.OpenSsl.PemReader]::new($reader)
            $request = [Org.BouncyCastle.Pkcs.Pkcs10CertificationRequest]$pemReader.ReadObject()
            $attributes = $request.GetCertificationRequestInfo().Attributes
            $extensionRequest = $null
            $attributeCount = if ($null -ne $attributes) { $attributes.Count } else { 0 }

            for ($i = 0; $i -lt $attributeCount; $i++) {
                $attribute = [Org.BouncyCastle.Asn1.Pkcs.AttributePkcs]::GetInstance($attributes[$i])
                if ($attribute.AttrType.Equals([Org.BouncyCastle.Asn1.Pkcs.PkcsObjectIdentifiers]::Pkcs9AtExtensionRequest)) {
                    $extensionRequest = $attribute
                    break
                }
            }

            $extensionRequest | Should -Not -BeNullOrEmpty

            $extensions = [Org.BouncyCastle.Asn1.X509.X509Extensions]::GetInstance($extensionRequest.AttrValues[0])
            $keyUsageExtension = $extensions.GetExtension([Org.BouncyCastle.Asn1.X509.X509Extensions]::KeyUsage)
            $keyUsageExtension | Should -Not -BeNullOrEmpty

            $keyUsage = [Org.BouncyCastle.Asn1.X509.KeyUsage]::GetInstance($keyUsageExtension.GetParsedValue())
            $expected = [Org.BouncyCastle.Asn1.X509.KeyUsage]::DigitalSignature -bor [Org.BouncyCastle.Asn1.X509.KeyUsage]::KeyEncipherment
            $keyUsage.IntValue | Should -Be $expected
        } finally {
            $reader.Dispose()
        }
    }

    It 'omits the CSR key usage extension when KeyUsage is not provided' {
        $csr = New-KrCertificateRequest -DnsNames 'localhost' -CommonName 'localhost'

        $csr | Should -Not -BeNullOrEmpty
        $csr.CsrPem | Should -Match 'BEGIN CERTIFICATE REQUEST'

        $reader = [System.IO.StringReader]::new($csr.CsrPem)
        try {
            $pemReader = [Org.BouncyCastle.OpenSsl.PemReader]::new($reader)
            $request = [Org.BouncyCastle.Pkcs.Pkcs10CertificationRequest]$pemReader.ReadObject()
            $attributes = $request.GetCertificationRequestInfo().Attributes
            $extensionRequest = $null
            $attributeCount = if ($null -ne $attributes) { $attributes.Count } else { 0 }

            for ($i = 0; $i -lt $attributeCount; $i++) {
                $attribute = [Org.BouncyCastle.Asn1.Pkcs.AttributePkcs]::GetInstance($attributes[$i])
                if ($attribute.AttrType.Equals([Org.BouncyCastle.Asn1.Pkcs.PkcsObjectIdentifiers]::Pkcs9AtExtensionRequest)) {
                    $extensionRequest = $attribute
                    break
                }
            }

            $extensionRequest | Should -Not -BeNullOrEmpty

            $extensions = [Org.BouncyCastle.Asn1.X509.X509Extensions]::GetInstance($extensionRequest.AttrValues[0])
            $keyUsageExtension = $extensions.GetExtension([Org.BouncyCastle.Asn1.X509.X509Extensions]::KeyUsage)
            $keyUsageExtension | Should -BeNullOrEmpty
        } finally {
            $reader.Dispose()
        }
    }
}
