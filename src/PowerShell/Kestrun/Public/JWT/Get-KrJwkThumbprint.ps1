<#
.SYNOPSIS
    Computes an RFC 7638 JWK thumbprint for an RSA key.

.DESCRIPTION
    Returns the Base64Url-encoded SHA-256 hash of the canonical JWK (RSA) built from the key's n and e values.
    You can provide either an X509Certificate2 (preferred) or a JWK hashtable with kty='RSA', n and e.

.PARAMETER Certificate
    X.509 certificate containing an RSA public key.

.PARAMETER Jwk
    Hashtable representing an RSA JWK with keys: kty, n, e.

.EXAMPLE
    Get-KrJwkThumbprint -Certificate $cert

.EXAMPLE
    Get-KrJwkThumbprint -Jwk @{ kty='RSA'; n=$n; e=$e }
#>
function Get-KrJwkThumbprint {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'Certificate')]
    [OutputType([string])]
    param(
        [Parameter(Mandatory, ParameterSetName = 'Certificate', Position = 0)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]
        $Certificate,

        [Parameter(Mandatory, ParameterSetName = 'Jwk', Position = 0)]
        [hashtable]
        $Jwk
    )

    switch ($PSCmdlet.ParameterSetName) {
        'Certificate' {
            return [Kestrun.Jwt.JwkUtilities]::ComputeThumbprintFromCertificate($Certificate)
        }
        'Jwk' {
            if (-not $Jwk.kty -or $Jwk.kty -ne 'RSA' -or -not $Jwk.n -or -not $Jwk.e) {
                throw "JWK must include kty='RSA', n and e"
            }
            return [Kestrun.Jwt.JwkUtilities]::ComputeThumbprintRsa([string]$Jwk.n, [string]$Jwk.e)
        }
    }
}
