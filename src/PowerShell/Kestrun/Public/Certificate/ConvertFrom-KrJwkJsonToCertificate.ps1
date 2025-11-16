<#
    .SYNOPSIS
        Creates a self-signed X509 certificate from an RSA JWK.
    .DESCRIPTION
        This function wraps
        [Kestrun.Certificates.CertificateManager]::CreateSelfSignedCertificateFromJwk()
        and converts an RSA JWK into a self-signed X509Certificate2 instance.

        The input can be:
          - a JWK JSON string, or
          - a PowerShell hashtable/PSCustomObject that will be serialized
            to JSON via ConvertTo-Json -Compress.

        Once you have the certificate, you can export it to PFX/PEM
        using Export-KrCertificate.
    .PARAMETER Jwk
        The JWK representation. Can be:
          - a JSON string, or
          - a hashtable / PSCustomObject with JWK fields (kty, n, e, d, p, q, dp, dq, qi, kid).
    .PARAMETER SubjectName
        Subject name for the self-signed certificate (CN=...). Defaults to "CN=client-jwt".
    .OUTPUTS
        [System.Security.Cryptography.X509Certificates.X509Certificate2]
    .EXAMPLE
        $jwk = @{
            kty = 'RSA'
            n   = '...'
            e   = 'AQAB'
            d   = '...'
            p   = '...'
            q   = '...'
            dp  = '...'
            dq  = '...'
            qi  = '...'
        }

        $cert = ConvertFrom-KrJwkJsonToCertificate -Jwk $jwk

    .EXAMPLE
        $jwkJson = Get-Content './client.jwk.json' -Raw
        $cert = ConvertFrom-KrJwkJsonToCertificate -Jwk $jwkJson -SubjectName 'CN=client-assertion'
    .EXAMPLE
        $jwk = Get-Content './client.jwk.json' -Raw
        ConvertFrom-KrJwkJsonToCertificate -Jwk $jwk |
            Export-KrCertificate -FilePath './certs/client' -Format Pem -IncludePrivateKey
#>
function ConvertFrom-KrJwkJsonToCertificate {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([System.Security.Cryptography.X509Certificates.X509Certificate2])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [object]
        $Jwk,

        [Parameter()]
        [string]
        $SubjectName = 'CN=client-jwt'
    )
    process {
        if ($null -eq $Jwk) {
            throw 'Jwk parameter cannot be null.'
        }

        # Normalize to JSON string
        if ($Jwk -is [string]) {
            $jwkJson = [string]$Jwk
        } else {
            # hashtable / PSCustomObject → JSON
            $jwkJson = $Jwk | ConvertTo-Json -Depth 10 -Compress
        }

        if ([string]::IsNullOrWhiteSpace($jwkJson)) {
            throw 'Resolved JWK JSON is empty.'
        }

        Write-KrLog -Level Verbose -Message 'Creating self-signed certificate from JWK (SubjectName={subjectName})' -Values $SubjectName

        return [Kestrun.Certificates.CertificateManager]::CreateSelfSignedCertificateFromJwk(
            $jwkJson,
            $SubjectName
        )
    }
}
