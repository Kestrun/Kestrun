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
    .EXAMPLE
        Test-KestrunCertificate -Certificate $cert -DenySelfSigned -CheckRevocation
    .EXAMPLE
        Test-KestrunCertificate -Certificate $cert -AllowWeakAlgorithms -ExpectedPurpose '1.3.6.1.5.5.7.3.1'
    .EXAMPLE
        Test-KestrunCertificate -Certificate $cert -StrictPurpose
        If specified, the certificate will be validated against these purposes.
    .NOTES
        This function is designed to be used in the context of Kestrun's certificate management.
        It leverages the Kestrun.Certificates.CertificateManager for validation.
#>
function Test-KsCertificate {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,

        [switch] $CheckRevocation,
        [switch] $AllowWeakAlgorithms,
        [switch] $DenySelfSigned,

        [string[]] $ExpectedPurpose,
        [switch] $StrictPurpose
    )

    $oidColl = if ($ExpectedPurpose) {
        $oc = [System.Security.Cryptography.OidCollection]::new()
        foreach ($p in $ExpectedPurpose) { $oc.Add([System.Security.Cryptography.Oid]::new($p)) }
        $oc
    } else { $null }

    return [Kestrun.Certificates.CertificateManager]::Validate($Certificate,
        $CheckRevocation.IsPresent,
        $AllowWeakAlgorithms.IsPresent,
        $DenySelfSigned.IsPresent,
        $oidColl,
        $StrictPurpose.IsPresent)
}

