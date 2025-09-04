<#
    .SYNOPSIS
        Lists the Enhanced Key Usage values on a certificate.
        This function is designed to be used in the context of Kestrun's certificate management.
    .DESCRIPTION
        Retrieves the Enhanced Key Usage (EKU) OIDs from a given X509Certificate2 object.
        The EKU values indicate the intended purposes of the certificate.
    .PARAMETER Certificate
        The X509Certificate2 object to retrieve the EKU values from.
    .EXAMPLE
        Get-KrCertificatePurpose -Certificate $cert
        This will return the Enhanced Key Usage values for the specified certificate.
    .NOTES
        This function is part of the Kestrun module.
#>
function Get-KrCertificatePurpose {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([System.Collections.Generic.IEnumerable`1[[System.String, System.Private.CoreLib]]])]
    param(
        [Parameter(Mandatory)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate
    )
    return [Kestrun.Certificates.CertificateManager]::GetPurposes($Certificate)
}

