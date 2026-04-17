<#
    .SYNOPSIS
        Validates a certificate’s chain, EKU, and cryptographic strength.
    .DESCRIPTION
        This function checks the validity of a given X509Certificate2 object by verifying its certificate chain,
        enhanced key usage (EKU), and cryptographic strength. It can also check for self-signed certificates and
        validate against expected purposes.
    .PARAMETER Certificate
        The X509Certificate2 object to validate.
    .PARAMETER CheckRevocation
        Indicates whether to check the certificate's revocation status.
    .PARAMETER AllowWeakAlgorithms
        Indicates whether to allow weak cryptographic algorithms.
    .PARAMETER DenySelfSigned
        Indicates whether to deny self-signed certificates.
    .PARAMETER ExpectedPurpose
        The expected purposes (OID) for the certificate.
        If specified, the certificate will be validated against these purposes.
    .PARAMETER StrictPurpose
        Indicates whether to enforce strict matching of the expected purposes.
    .PARAMETER CertificateChain
        Optional additional certificates used to build trust for the target certificate, such as
        a private development root CA or intermediate certificates.
    .PARAMETER FailureReasonVariable
        Optional variable name that will receive the validation failure reason in the caller scope.
        When validation succeeds, the target variable is set to an empty string.
    .EXAMPLE
        Test-KestrunCertificate -Certificate $cert -DenySelfSigned -CheckRevocation
    .EXAMPLE
        Test-KestrunCertificate -Certificate $cert -AllowWeakAlgorithms -ExpectedPurpose '1.3.6.1.5.5.7.3.1'
    .EXAMPLE
        Test-KestrunCertificate -Certificate $cert -StrictPurpose
        If specified, the certificate will be validated against these purposes.
    .EXAMPLE
        $bundle = New-KrDevelopmentCertificate -Exportable
        $isValid = Test-KrCertificate -Certificate $bundle.LeafCertificate -CertificateChain $bundle.RootCertificate -FailureReasonVariable 'reason'
        if (-not $isValid) { Write-Host "Validation failed: $reason" }
    .EXAMPLE
        $isValid = Test-KrCertificate -Certificate $cert -FailureReasonVariable 'reason'
        if (-not $isValid) { Write-Host "Validation failed: $reason" }
    .NOTES
        This function is designed to be used in the context of Kestrun's certificate management.
        It leverages the Kestrun.Certificates.CertificateManager for validation.
#>
function Test-KrCertificate {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,

        [Parameter()]
        [switch]$CheckRevocation,

        [Parameter()]
        [switch]$AllowWeakAlgorithms,

        [Parameter()]
        [switch]$DenySelfSigned,

        [Parameter()]
        [string[]]$ExpectedPurpose,

        [Parameter()]
        [switch]$StrictPurpose,

        [Parameter()]
        [System.Security.Cryptography.X509Certificates.X509Certificate2[]]$CertificateChain,

        [Parameter()]
        [string]$FailureReasonVariable
    )

    $oidColl = if ($ExpectedPurpose) {
        $oc = [System.Security.Cryptography.OidCollection]::new()
        foreach ($p in $ExpectedPurpose) { $oc.Add([System.Security.Cryptography.Oid]::new($p)) }
        $oc
    } else { $null }

    $chainCollection = if ($CertificateChain) {
        $collection = [System.Security.Cryptography.X509Certificates.X509Certificate2Collection]::new()
        foreach ($chainCertificate in $CertificateChain) {
            [void]$collection.Add($chainCertificate)
        }
        $collection
    } else { $null }

    $reason = ''
    $isValid = [Kestrun.Certificates.CertificateManager]::Validate($Certificate,
        $CheckRevocation.IsPresent,
        $AllowWeakAlgorithms.IsPresent,
        $DenySelfSigned.IsPresent,
        $oidColl,
        $StrictPurpose.IsPresent,
        $chainCollection,
        [ref]$reason)

    if ($PSBoundParameters.ContainsKey('FailureReasonVariable')) {
        if ([string]::IsNullOrWhiteSpace($FailureReasonVariable)) {
            throw 'FailureReasonVariable cannot be null or whitespace when provided.'
        }

        if ($FailureReasonVariable -match '^[A-Za-z]+:') {
            Set-Variable -Name $FailureReasonVariable -Value $reason -Force
        } else {
            Set-Variable -Name $FailureReasonVariable -Scope 2 -Value $reason -Force
        }
    }

    return $isValid
}

