<#
    .SYNOPSIS
        Exports an X509Certificate2 to PFX or PEM(+key).
    .DESCRIPTION
        This function exports a given X509Certificate2 object to a specified file path in either PFX or PEM format.
        If the PEM format is chosen and the IncludePrivateKey switch is set, it will also export the private key.
    .PARAMETER Certificate
        The X509Certificate2 object to export.
    .PARAMETER FilePath
        The file path to export the certificate to (without extension).
    .PARAMETER Format
        The export format (Pfx or Pem).
    .PARAMETER Password
        The password to protect the exported PFX file (if applicable).
    .PARAMETER IncludePrivateKey
        Whether to include the private key in the export (only applicable for PEM format).

    .EXAMPLE
        Export-KrCertificate -Certificate $cert -FilePath 'C:\certs\my' `
            -Format Pem -Password 'p@ss' -IncludePrivateKey
    .NOTES
        This function requires the Kestrun module to be imported.
#>
function Export-KrCertificate {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [ValidateSet('Pfx', 'Pem')]
        [string] $Format = 'Pfx',
        [securestring] $Password,
        [switch] $IncludePrivateKey
    )
    process {
        if ($null -eq $Certificate) {
            throw "Certificate parameter is required."
        }
        if ([string]::IsNullOrWhiteSpace($FilePath)) {
            throw "FilePath parameter is required."
        }
        $resolvedPath = Resolve-KrPath -Path $FilePath -KestrunRoot
        Write-KrLog -Level Verbose -Message "Resolved file path: $resolvedPath"

        $fmtEnum = [Kestrun.Certificates.CertificateManager+ExportFormat]::$Format
        [Kestrun.Certificates.CertificateManager]::Export($Certificate, $resolvedPath, $fmtEnum, $Password,
            $IncludePrivateKey.IsPresent)
        Write-KrLog -Level Verbose -Message "Certificate exported to $resolvedPath with format $Format"
    }
}

