<#
    .SYNOPSIS
        Creates a self-signed certificate or localhost development certificate bundle.
    .DESCRIPTION
        New-KrSelfSignedCertificate generates a single self-signed certificate for development or testing,
        or, when -Development is specified, creates a localhost development bundle consisting of a CA
        root certificate and an issued leaf certificate. On Windows, you can optionally trust the
        generated or supplied development root certificate in the CurrentUser Root store.
    .PARAMETER DnsNames
        The DNS name(s) for the certificate. In development mode, if omitted, localhost loopback names
        are used by default.
    .PARAMETER KeyType
        The type of key to use for the certificate (RSA or ECDSA).
    .PARAMETER KeyLength
        The length of the key in bits (only applicable for RSA).
    .PARAMETER ValidDays
        The number of days the (non-development) certificate will be valid.
        In development mode, use -LeafValidDays and -RootValidDays.
    .PARAMETER KeyUsage
        Optional X.509 Key Usage flags to apply to the certificate.
    .PARAMETER CertificateAuthority
        Creates a CA certificate suitable for signing child certificates.
    .PARAMETER IssuerCertificate
        An optional issuer/root certificate used to sign the generated certificate. The issuer must include a private key.
    .PARAMETER Development
        Creates a localhost development bundle consisting of a CA root certificate and an issued leaf certificate.
    .PARAMETER RootCertificate
        An optional CA root certificate used to sign the generated development leaf certificate.
    .PARAMETER RootName
        The subject common name to use when creating a new development root certificate.
    .PARAMETER LeafValidDays
        The number of days the generated development leaf certificate is valid.
    .PARAMETER RootValidDays
        The number of days a generated development root certificate is valid.
    .PARAMETER TrustRoot
        If specified with -Development on Windows, adds the development root certificate to the CurrentUser Root store.
        On non-Windows platforms, this cmdlet writes a warning and continues without trusting the root certificate.
    .PARAMETER WhatIf
        When -TrustRoot is specified, shows the pending trust-store change and skips adding the
        development root to the Windows CurrentUser Root certificate store.
    .PARAMETER Confirm
        When -TrustRoot is specified, prompts for confirmation before adding the development root
        certificate to the Windows CurrentUser Root certificate store.
    .PARAMETER Ephemeral
        Indicates whether the certificate is ephemeral (temporary).
    .PARAMETER Exportable
        Indicates whether the private key is exportable.
    .EXAMPLE
        New-KrSelfSignedCertificate -DnsNames 'example.com' -KeyUsage DigitalSignature,KeyEncipherment

        This example creates a self-signed certificate and applies explicit key-usage flags using PowerShell-friendly enum array syntax.
    .EXAMPLE
        $bundle = New-KrSelfSignedCertificate -Development -TrustRoot

        Creates a development root CA, issues a localhost leaf certificate from it, trusts the root in the
        CurrentUser Root store on Windows, and returns the private root, public-only root, and leaf certificates.
    .EXAMPLE
        $root = Import-KrCertificate -FilePath './certs/dev-root.pfx' -Password $password
        $bundle = New-KrSelfSignedCertificate -Development -RootCertificate $root -DnsNames 'localhost','127.0.0.1','::1'

        Reuses an existing development root certificate to issue a new localhost leaf certificate.
    .NOTES
        This function is intended for use in development and testing environments only. Do not use self-signed certificates in production.
#>
function New-KrSelfSignedCertificate {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'Standard', SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    [OutputType([System.Security.Cryptography.X509Certificates.X509Certificate2], ParameterSetName = 'Standard')]
    [OutputType([object], ParameterSetName = 'Development')]
    param(
        [Parameter(Mandatory, ParameterSetName = 'Standard')]
        [Parameter(ParameterSetName = 'Development')]
        [string[]]$DnsNames,

        [Parameter(ParameterSetName = 'Standard')]
        [ValidateSet('Rsa', 'Ecdsa')]
        [string]$KeyType = 'Rsa',

        [Parameter(ParameterSetName = 'Standard')]
        [ValidateRange(256, 8192)]
        [int]$KeyLength = 2048,

        [Parameter(ParameterSetName = 'Standard')]
        [ValidateRange(1, 3650)]
        [int]$ValidDays = 365,

        [Parameter(ParameterSetName = 'Standard')]
        [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags[]]$KeyUsage = @(),

        [Parameter(ParameterSetName = 'Standard')]
        [Alias('IsCertificateAuthority')]
        [switch]$CertificateAuthority,

        [Parameter(ParameterSetName = 'Standard')]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$IssuerCertificate,

        [Parameter(ParameterSetName = 'Development', Mandatory)]
        [switch]$Development,

        [Parameter(ParameterSetName = 'Development')]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$RootCertificate,

        [Parameter(ParameterSetName = 'Development')]
        [string]$RootName = 'Kestrun Development Root CA',

        [Parameter(ParameterSetName = 'Development')]
        [ValidateRange(1, 3650)]
        [int]$LeafValidDays = 30,

        [Parameter(ParameterSetName = 'Development')]
        [ValidateRange(1, 36500)]
        [int]$RootValidDays = 3650,

        [Parameter(ParameterSetName = 'Development')]
        [switch]$TrustRoot,

        [Parameter(ParameterSetName = 'Standard')]
        [switch]$Ephemeral,

        [Parameter()]
        [switch]$Exportable
    )

    $keyUsageFlags = if ($PSBoundParameters.ContainsKey('KeyUsage') -and $KeyUsage.Count -gt 0) {
        Join-KeyUsageFlag -KeyUsage $KeyUsage
    }

    $trustRoot = $false
    if ($TrustRoot.IsPresent) {
        if (-not $IsWindows) {
            Write-KrLog -level Warning `
                -Message ('The -TrustRoot parameter is only supported on Windows. The development certificate will be created without trusting the root certificate." +
                "Trust the root certificate manually using your platform certificate store tools.')
        } else {
            $trustTarget = if ($PSBoundParameters.ContainsKey('RootCertificate') -and $null -ne $RootCertificate) {
                $RootCertificate.Subject
            } else {
                "development root certificate '$RootName'"
            }

            if ($PSCmdlet.ShouldProcess($trustTarget, 'Trust in Windows CurrentUser Root certificate store')) {
                $trustRoot = $true
            }
        }
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
        $IssuerCertificate,
        $Development.IsPresent,
        $RootCertificate,
        $RootName,
        $LeafValidDays,
        $RootValidDays,
        $trustRoot
    )

    $result = [Kestrun.Certificates.CertificateManager]::NewSelfSigned($opts)

    if ($Development.IsPresent) {
        return $result
    }

    return $result.Certificate
}
