<#
    .SYNOPSIS
        Imports a PFX/PEM certificate file and returns X509Certificate2.
    .DESCRIPTION
        The Import-KsCertificate function allows you to import a certificate into the Kestrun environment.
        This may include loading a certificate from a file or other source and adding it to the appropriate certificate store or configuration.
    .PARAMETER FilePath
        The path to the certificate file to import.
    .PARAMETER Password
        The password for the certificate file, if applicable.
    .PARAMETER PrivateKeyPath
        The path to the private key file, if applicable.
    .EXAMPLE
        Import-KsCertificate -Path "C:\certs\mycert.pfx" -Password (ConvertTo-SecureString "password" -AsPlainText -Force)
        This example imports a certificate from the specified path using the provided password.
    .NOTES
        This function is part of the Kestrun PowerShell module.
#>
function Import-KsCertificate {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([System.Security.Cryptography.X509Certificates.X509Certificate2])]
    param(
        [Parameter(Mandatory)][string] $FilePath,
        [securestring] $Password,
        [string] $PrivateKeyPath
    )
    $resolvedPath = Resolve-KrPath -Path $FilePath -KestrunRoot -Test
    Write-KrVerboseLog -Message "Resolved file path: $resolvedPath"
    if ($null -eq $Password) {
        return [Kestrun.Certificates.CertificateManager]::Import($resolvedPath, $PrivateKeyPath)
    }
    return [Kestrun.Certificates.CertificateManager]::Import($resolvedPath, $Password, $PrivateKeyPath)
}

