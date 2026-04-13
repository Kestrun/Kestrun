<#
.SYNOPSIS
    Creates a PEM-encoded CSR (and returns the private key).
.DESCRIPTION
    Creates a PEM-encoded CSR (Certificate Signing Request) and returns the private key.
    The CSR can be used to request a certificate from a CA (Certificate Authority).
.PARAMETER DnsNames
    The DNS name(s) for which the certificate is requested.
    This can include multiple names for Subject Alternative Names (SANs).
.PARAMETER KeyType
    The type of key to generate for the CSR. Options are 'Rsa' or 'Ecdsa'.
    Defaults to 'Rsa'.
.PARAMETER KeyLength
    The length of the key to generate. Defaults to 2048 bits for RSA keys.
    This parameter is ignored for ECDSA keys.
.PARAMETER Country
    The country name (2-letter code) to include in the CSR.
    This is typically the ISO 3166-1 alpha-2 code (e.g., 'US' for the United States).
.PARAMETER Org
    The organization name to include in the CSR.
    This is typically the legal name of the organization.
.PARAMETER OrgUnit
    The organizational unit name to include in the CSR.
    This is typically the department or division within the organization.
.PARAMETER CommonName
    The common name (CN) to include in the CSR.
    This is typically the fully qualified domain name (FQDN) for the certificate.
.PARAMETER KeyUsage
    Optional X.509 key usage flags to include in the CSR extension request.
    Use this when the target CA or downstream tooling expects explicit key-usage hints in the CSR.
.OUTPUTS
    [Kestrun.Certificates.CsrResult]

.EXAMPLE
    $csr, $priv = New-KestrunCertificateRequest -DnsNames 'example.com' -Country US
    $csr | Set-Content -Path 'C:\path\to\csr.pem'
    $priv | Set-Content -Path 'C:\path\to\private.key'
.EXAMPLE
    $csr, $priv = New-KestrunCertificateRequest -DnsNames 'example.com' -Country US -Org 'Example Corp' -OrgUnit 'IT' -CommonName 'example.com'
    $csr | Set-Content -Path 'C:\path\to\csr.pem'
    $priv | Set-Content -Path 'C:\path\to\private.key'
.EXAMPLE
    $csr = New-KrCertificateRequest -DnsNames 'example.com' -CommonName 'example.com' -KeyUsage DigitalSignature,KeyEncipherment

    Creates a CSR that includes an explicit key-usage extension request.
#>
function New-KrCertificateRequest {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Kestrun.Certificates.CsrResult])]
    param(
        [Parameter(Mandatory)]
        [string[]] $DnsNames,

        [Parameter()]
        [ValidateSet('Rsa', 'Ecdsa')]
        [string]$KeyType = 'Rsa',

        [Parameter()]
        [int]$KeyLength = 2048,

        [Parameter()]
        [string]$Country,

        [Parameter()]
        [string]$Org,

        [Parameter()]
        [string]$OrgUnit,

        [Parameter()]
        [string]$CommonName,
        
        [Parameter()]
        [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags[]]$KeyUsage = @()
    )

    $keyUsageFlags = $null
    if ($PSBoundParameters.ContainsKey('KeyUsage') -and $KeyUsage.Count -gt 0) {
        $combinedKeyUsage = [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::None
        foreach ($keyUsageFlag in $KeyUsage) {
            $combinedKeyUsage = $combinedKeyUsage -bor $keyUsageFlag
        }

        if ($combinedKeyUsage -ne [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::None) {
            $keyUsageFlags = $combinedKeyUsage
        }
    }

    $opts = [Kestrun.Certificates.CsrOptions]::new(
        $DnsNames,
        [Kestrun.Certificates.KeyType]::$KeyType,
        $KeyLength,
        $Country,
        $Org,
        $OrgUnit,
        $CommonName,
        $keyUsageFlags
    )
    return [Kestrun.Certificates.CertificateManager]::NewCertificateRequest($opts)
}
