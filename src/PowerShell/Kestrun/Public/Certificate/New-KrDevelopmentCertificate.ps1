<#
    .SYNOPSIS
        Creates a localhost development certificate bundle.
    .DESCRIPTION
        New-KrDevelopmentCertificate creates a localhost leaf certificate for Kestrun development and,
        when needed, also creates a CA root certificate that can sign the leaf. You can optionally
        trust the generated or supplied root certificate in the Windows CurrentUser Root store.
    .PARAMETER DnsNames
        The localhost names/IPs to include in the development leaf certificate SAN extension.
    .PARAMETER RootCertificate
        An optional CA root certificate used to sign the localhost leaf. If omitted, a new development
        root certificate is created.
    .PARAMETER RootName
        The subject common name to use when creating a new development root certificate.
    .PARAMETER RootValidDays
        The number of days a generated development root certificate is valid.
    .PARAMETER LeafValidDays
        The number of days the localhost leaf certificate is valid.
    .PARAMETER TrustRoot
        If specified on Windows, adds the development root certificate to the CurrentUser Root store.
    .PARAMETER Exportable
        If specified, the generated certificates will use exportable private keys.
    .PARAMETER WhatIf
        When -TrustRoot is specified, shows the pending trust-store change and skips adding the
        development root to the Windows CurrentUser Root certificate store.
    .PARAMETER Confirm
        When -TrustRoot is specified, prompts for confirmation before adding the development root
        certificate to the Windows CurrentUser Root certificate store.
    .EXAMPLE
        $bundle = New-KrDevelopmentCertificate -TrustRoot

        Creates a development root CA, issues a localhost leaf certificate from it, trusts the root
        in the CurrentUser Root store on Windows, and returns both certificates.
    .EXAMPLE
        $root = Import-KrCertificate -FilePath './certs/dev-root.pfx' -Password $password
        $bundle = New-KrDevelopmentCertificate -RootCertificate $root -DnsNames 'localhost','127.0.0.1','::1'

        Reuses an existing development root certificate to issue a new localhost leaf certificate.
    .EXAMPLE
        $password = ConvertTo-SecureString 'p@ssw0rd!' -AsPlainText -Force
        $bundle = New-KrDevelopmentCertificate -Exportable

        Export-KrCertificate -Certificate $bundle.RootCertificate -FilePath './certs/dev-root' -Format Pfx -Password $password -IncludePrivateKey
        Export-KrCertificate -Certificate $bundle.LeafCertificate -FilePath './certs/localhost' -Format Pfx -Password $password -IncludePrivateKey

        Exports the generated root and localhost leaf certificates to PFX files so they can be reused in later sessions.
    .OUTPUTS
        Kestrun.Certificates.DevelopmentCertificateResult.
#>
function New-KrDevelopmentCertificate {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    [OutputType([Kestrun.Certificates.DevelopmentCertificateResult])]
    param(
        [Parameter()]
        [string[]]$DnsNames = @('localhost', '127.0.0.1', '::1'),

        [Parameter()]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$RootCertificate,

        [Parameter()]
        [string]$RootName = 'Kestrun Development Root CA',

        [Parameter()]
        [ValidateRange(1, 36500)]
        [int]$RootValidDays = 3650,

        [Parameter()]
        [ValidateRange(1, 3650)]
        [int]$LeafValidDays = 30,

        [Parameter()]
        [switch]$TrustRoot,

        [Parameter()]
        [switch]$Exportable
    )

    $trustRoot = $TrustRoot.IsPresent
    if ($trustRoot) {
        $trustTarget = if ($PSBoundParameters.ContainsKey('RootCertificate') -and $null -ne $RootCertificate) {
            $RootCertificate.Subject
        } else {
            "development root certificate '$RootName'"
        }

        if (-not $PSCmdlet.ShouldProcess($trustTarget, 'Trust in Windows CurrentUser Root certificate store')) {
            $trustRoot = $false
        }
    }

    $options = [Kestrun.Certificates.DevelopmentCertificateOptions]::new(
        $DnsNames,
        $RootCertificate,
        $RootName,
        $RootValidDays,
        $LeafValidDays,
        $trustRoot,
        $Exportable.IsPresent)

    return [Kestrun.Certificates.CertificateManager]::NewDevelopmentCertificate($options)
}
