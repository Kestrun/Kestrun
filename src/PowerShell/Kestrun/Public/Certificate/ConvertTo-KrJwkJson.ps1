<#
    .SYNOPSIS
        Converts an X509 certificate or RSA private key PEM to an RSA JWK JSON string.
    .DESCRIPTION
        This function converts either:
          - an X509 certificate object, or
          - an RSA private key in PEM format
        to a JSON Web Key (JWK) representation using the
        Kestrun.Certificates.CertificateManager backend.

        For certificates:
          - By default it exports only the public parameters (suitable for JWKS).
          - If -IncludePrivateParameters is specified, the private RSA parameters
            are included as well (for local/secure use only; never publish those).

        For RSA private key PEM:
          - The output is always a full private JWK (public + private parameters),
            as the source is inherently private key material.
    .PARAMETER Certificate
        The X509Certificate2 object to convert to JWK JSON.
        Typically obtained from Import-KrCertificate and passed via the pipeline.
    .PARAMETER RsaPrivateKeyPem
        A string containing an RSA private key in PEM format
        (e.g. "-----BEGIN RSA PRIVATE KEY----- ...").
    .PARAMETER RsaPrivateKeyPath
        Path to a file containing an RSA private key PEM. The file is read as raw text
        and passed to CertificateManager.CreateJwkJsonFromRsaPrivateKeyPem().
    .PARAMETER IncludePrivateParameters
        When converting from a certificate:
            If specified, includes private RSA parameters (d, p, q, dp, dq, qi) in the JWK JSON.
            Requires that the certificate has a private key.
        When converting from RSA private key PEM:
            Ignored. The output always contains private parameters because the source
            is a private key.
    .OUTPUTS
        [string] – the JWK JSON string.
    .EXAMPLE
        Import-KrCertificate -Path './certs/client.pfx' |
            ConvertTo-KrJwkJson

        Imports a certificate and converts it to a public-only JWK JSON string.
    .EXAMPLE
        Import-KrCertificate -Path './certs/client.pfx' |
            ConvertTo-KrJwkJson -IncludePrivateParameters

        Imports the certificate and returns a full private JWK JSON string.
    .EXAMPLE
        $pem = Get-Content './Assets/certs/private.pem' -Raw
        ConvertTo-KrJwkJson -RsaPrivateKeyPem $pem

        Converts the RSA private key PEM to a full private JWK JSON string.
    .EXAMPLE
        ConvertTo-KrJwkJson -RsaPrivateKeyPath './Assets/certs/private.pem'

        Reads the RSA private key PEM from disk and converts it to a full private JWK JSON string.
    .NOTES
        Requires the Kestrun module and the Kestrun.Certificates assembly to be loaded.
#>
function ConvertTo-KrJwkJson {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'ByCertificate')]
    [OutputType([string])]
    param(
        # Certificate instance (from pipeline)
        [Parameter(Mandatory = $true, ValueFromPipeline = $true, ParameterSetName = 'ByCertificate')]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]
        $Certificate,

        # Raw RSA private key PEM
        [Parameter(Mandatory = $true, ParameterSetName = 'ByRsaPrivateKeyPem')]
        [string]
        $RsaPrivateKeyPem,

        # RSA private key PEM file path
        [Parameter(Mandatory = $true, ParameterSetName = 'ByRsaPrivateKeyPath')]
        [string]
        $RsaPrivateKeyPath,

        [Parameter()]
        [switch]
        $IncludePrivateParameters
    )
    process {
        switch ($PSCmdlet.ParameterSetName) {
            'ByCertificate' {
                $cert = $Certificate

                if ($null -eq $cert) {
                    throw "Failed to obtain certificate instance."
                }

                if ($IncludePrivateParameters.IsPresent -and -not $cert.HasPrivateKey) {
                    throw "Certificate does not contain a private key, cannot include private parameters in JWK."
                }

                $json = [Kestrun.Certificates.CertificateManager]::CreateJwkJsonFromCertificate(
                    $cert,
                    $IncludePrivateParameters.IsPresent
                )

                Write-KrLog -Level Verbose -Message "Converted certificate to JWK JSON (IncludePrivateParameters=$($IncludePrivateParameters.IsPresent))"
                $json
            }

            'ByRsaPrivateKeyPem' {
                if ([string]::IsNullOrWhiteSpace($RsaPrivateKeyPem)) {
                    throw "RsaPrivateKeyPem parameter cannot be null or empty."
                }

                # For private key PEM we always get a full private JWK.
                $json = [Kestrun.Certificates.CertificateManager]::CreateJwkJsonFromRsaPrivateKeyPem(
                    $RsaPrivateKeyPem,
                    $null
                )

                Write-KrLog -Level Verbose -Message "Converted RSA private key PEM (inline) to JWK JSON (private key material included)."
                $json
            }

            'ByRsaPrivateKeyPath' {
                if ([string]::IsNullOrWhiteSpace($RsaPrivateKeyPath)) {
                    throw "RsaPrivateKeyPath parameter cannot be null or empty."
                }

                $resolved = Resolve-KrPath -Path $RsaPrivateKeyPath -KestrunRoot
                if (-not (Test-Path -LiteralPath $resolved)) {
                    throw "RSA private key PEM file not found: $resolved"
                }

                $pem = Get-Content -LiteralPath $resolved -Raw
                $json = [Kestrun.Certificates.CertificateManager]::CreateJwkJsonFromRsaPrivateKeyPem(
                    $pem,
                    $null
                )

                Write-KrLog -Level Verbose -Message "Converted RSA private key PEM from '$resolved' to JWK JSON (private key material included)."
                $json
            }
        }
    }
}
