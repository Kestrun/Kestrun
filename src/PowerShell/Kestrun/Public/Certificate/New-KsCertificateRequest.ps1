<#
    .SYNOPSIS
        Creates a PEM-encoded CSR (and returns the private key).

    .DESCRIPTION
        Creates a PEM-encoded CSR (Certificate Signing Request) and returns the private key.
        The CSR can be used to request a certificate from a CA (Certificate Authority).
    .PARAMETER DnsName
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
    .OUTPUTS
        [Kestrun.Certificates.CertificateManager.CsrResult]

    .EXAMPLE
        $csr, $priv = New-KestrunCertificateRequest -DnsName 'example.com' -Country US
        $csr | Set-Content -Path 'C:\path\to\csr.pem'
        $priv | Set-Content -Path 'C:\path\to\private.key'
    .EXAMPLE
        $csr, $priv = New-KestrunCertificateRequest -DnsName 'example.com' -Country US -Org 'Example Corp' -OrgUnit 'IT' -CommonName 'example.com'
        $csr | Set-Content -Path 'C:\path\to\csr.pem'
        $priv | Set-Content -Path 'C:\path\to\private.key'

#>
function New-KsCertificateRequest {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Kestrun.Certificates.CsrResult])]
    param(
        [Parameter(Mandatory)]
        [string[]] $DnsName,

        [ValidateSet('Rsa', 'Ecdsa')]
        [string]   $KeyType = 'Rsa',

        [int]      $KeyLength = 2048,

        [string]   $Country,
        [string]   $Org,
        [string]   $OrgUnit,
        [string]   $CommonName
    )

    $opts = [Kestrun.Certificates.CertificateManager+CsrOptions]::new(
        $DnsName,
        [Kestrun.Certificates.CertificateManager+KeyType]::$KeyType,
        $KeyLength,
        $Country,
        $Org,
        $OrgUnit,
        $CommonName
    )
    return [Kestrun.Certificates.CertificateManager]::NewCertificateRequest($opts)
}

