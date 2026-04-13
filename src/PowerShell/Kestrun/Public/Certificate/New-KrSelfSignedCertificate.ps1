<#
    .SYNOPSIS
        Creates a new self-signed certificate.
    .DESCRIPTION
        The New-KrSelfSignedCertificate function generates a self-signed certificate for use in development or testing scenarios.
        This certificate can be used for securing communications or authentication purposes.
    .PARAMETER DnsNames
        The DNS name(s) for the certificate.
    .PARAMETER KeyType
        The type of key to use for the certificate (RSA or ECDSA).
    .PARAMETER KeyLength
        The length of the key in bits (only applicable for RSA).
    .PARAMETER ValidDays
        The number of days the certificate will be valid.
    .PARAMETER KeyUsage
        Optional X.509 Key Usage flags to apply to the certificate.
    .PARAMETER CertificateAuthority
        Creates a CA certificate suitable for signing child certificates.
    .PARAMETER IssuerCertificate
        An optional issuer/root certificate used to sign the generated certificate. The issuer must include a private key.
    .PARAMETER Ephemeral
        Indicates whether the certificate is ephemeral (temporary).
    .PARAMETER Exportable
        Indicates whether the private key is exportable.
    .EXAMPLE
        New-KrSelfSignedCertificate -DnsNames 'example.com' -KeyUsage DigitalSignature,KeyEncipherment

        This example creates a self-signed certificate and applies explicit key-usage flags using PowerShell-friendly enum array syntax.
    .NOTES
        This function is intended for use in development and testing environments only. Do not use self-signed certificates in production.
#>
function New-KrSelfSignedCertificate {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([System.Security.Cryptography.X509Certificates.X509Certificate2])]
    param(
        [Parameter(Mandatory)]
        [string[]]$DnsNames,

        [ValidateSet('Rsa', 'Ecdsa')]
        [string]$KeyType = 'Rsa',

        [Parameter()]
        [ValidateRange(256, 8192)]
        [int]$KeyLength = 2048,

        [Parameter()]
        [ValidateRange(1, 3650)]
        [int]$ValidDays = 365,

        [Parameter()]
        [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags[]]$KeyUsage = @(),

        [Parameter()]
        [Alias('IsCertificateAuthority')]
        [switch]$CertificateAuthority,

        [Parameter()]
        [Alias('RootCertificate')]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$IssuerCertificate,

        [Parameter()]
        [switch]$Ephemeral,

        [Parameter()]
        [switch]$Exportable
    )

    $keyUsageFlags = if ($PSBoundParameters.ContainsKey('KeyUsage') -and $KeyUsage.Count -gt 0) {
        Join-KeyUsageFlags -KeyUsage $KeyUsage
    }

    $opts = [Kestrun.Certificates.SelfSignedOptions]::new(
        $DnsNames,
        [Kestrun.Certificates.KeyType]::$KeyType,
        $KeyLength,
        $null,      # purposes
        $keyUsageFlags,
        $ValidDays,
        $Ephemeral.IsPresent,
        $Exportable.IsPresent,
        $CertificateAuthority.IsPresent,
        $IssuerCertificate
    )

    return [Kestrun.Certificates.CertificateManager]::NewSelfSigned($opts)
}
