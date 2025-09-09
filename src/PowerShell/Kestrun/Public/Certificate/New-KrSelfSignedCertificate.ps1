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
    .PARAMETER Ephemeral
        Indicates whether the certificate is ephemeral (temporary).
    .PARAMETER Exportable
        Indicates whether the private key is exportable.
    .EXAMPLE
        New-KrSelfSignedCertificate -Subject "CN=MyCert" -CertStoreLocation "Cert:\LocalMachine\My"

        This example creates a self-signed certificate with the subject "CN=MyCert" and stores it in the local machine's certificate store.
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

        [ValidateRange(256, 8192)]
        [int]$KeyLength = 2048,

        [ValidateRange(1, 3650)]
        [int]$ValidDays = 365,

        [switch]$Ephemeral,
        [switch]$Exportable
    )

    $opts = [Kestrun.Certificates.CertificateManager+SelfSignedOptions]::new(
        $DnsNames,
        [Kestrun.Certificates.CertificateManager+KeyType]::$KeyType,
        $KeyLength,
        $null,      # purposes
        $ValidDays,
        $Ephemeral.IsPresent,
        $Exportable.IsPresent
    )

    return [Kestrun.Certificates.CertificateManager]::NewSelfSigned($opts)
}

