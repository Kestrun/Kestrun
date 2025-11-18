<#
    .SYNOPSIS
        Encrypts the JWT payload using a secret, PEM file, or X509 certificate.

    .DESCRIPTION
        Protect-KrJWTPayload configures a JWT token builder to encrypt the payload using a variety of key sources:
        - Base64Url-encoded secret
        - Hexadecimal key
        - Raw byte array
        - PEM file containing an RSA public key
        - X509 certificate

        The function ensures confidentiality of the JWT payload by applying encryption with the specified key and algorithms.

    .PARAMETER Builder
        The JWT token builder to modify.

    .PARAMETER HexadecimalKey
        The hexadecimal key used to encrypt the JWT token payload.

    .PARAMETER Base64Url
        The Base64Url-encoded secret used to encrypt the JWT token payload.

    .PARAMETER KeyBytes
        The byte array used to encrypt the JWT token payload.

    .PARAMETER KeyAlg
        The key algorithm to use for encryption (e.g., "HS256", "RS256"). Optional.

    .PARAMETER EncAlg
        The encryption algorithm to use (e.g., "A256GCM"). Optional.

    .PARAMETER PemPath
        The path to a PEM file containing the RSA public key for encryption.

    .PARAMETER X509Certificate
        The X509 certificate used for encryption.

    .PARAMETER JwkJson
        The JSON Web Key (JWK) in JSON format used for encryption.

    .PARAMETER JwkPath
        The path to a JSON Web Key (JWK) file used for encryption.

    .OUTPUTS
        [Kestrun.Jwt.JwtTokenBuilder]
        Returns the modified JWT token builder with encryption applied.

    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Protect-KrJWTPayload -Base64Url "your_base64_url_secret"
        $builder | Protect-KrJWTPayload -HexadecimalKey "a1b2c3d4e5f6"
        $builder | Protect-KrJWTPayload -KeyBytes (Get-Content -Path "C:\path\to\key.bin" -Encoding Byte)
        $builder | Protect-KrJWTPayload -KeyAlg "HS256" -EncAlg "A256GCM"
        $builder | Protect-KrJWTPayload -PemPath "C:\path\to\key.pem"
        $builder | Protect-KrJWTPayload -X509Certificate (Get-Item "C:\path\to\certificate.pfx")

    .NOTES
        This function is part of the Kestrun.Jwt module and is used to build and protect JWT tokens.
        Internally maps to JwtTokenBuilder.EncryptWithSecretB64, EncryptWithSecretHex, EncryptWithSecret,
        EncryptWithPemPublic, and EncryptWithCertificate methods.

    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
#>
function Protect-KrJWTPayload {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'Base64Url')]
    [OutputType([Kestrun.Jwt.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Jwt.JwtTokenBuilder] $Builder,

        [Parameter(Mandatory = $true, ParameterSetName = 'HexadecimalKey')]
        [string] $HexadecimalKey,

        [Parameter(Mandatory = $true, ParameterSetName = 'Base64Url')]
        [string] $Base64Url,

        [Parameter(Mandatory = $true, ParameterSetName = 'Bytes')]
        [byte[]] $KeyBytes,

        [Parameter(Mandatory = $false)]
        [string] $KeyAlg = '',

        [Parameter(Mandatory = $false)]
        [string] $EncAlg = '',

        [Parameter(Mandatory = $true, ParameterSetName = 'PemPath')]
        [string] $PemPath,

        [Parameter(Mandatory = $true, ParameterSetName = 'Certificate')]
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $X509Certificate,

        [Parameter(Mandatory = $true, ParameterSetName = 'JwkJson')]
        [string] $JwkJson,

        [Parameter(Mandatory = $true, ParameterSetName = 'JwkPath')]
        [string] $JwkPath
    )

    process {
        # Determine defaults based on parameter set (symmetric vs asymmetric)
        $defaultKeyAlg, $defaultEncAlg = switch ($PSCmdlet.ParameterSetName) {
            'Base64Url' { 'dir', 'A256CBC-HS512' }
            'HexadecimalKey' { 'dir', 'A256CBC-HS512' }
            'Bytes' { 'dir', 'A256CBC-HS512' }
            default { 'RSA-OAEP', 'A256GCM' }
        }

        $ka = if ([string]::IsNullOrWhiteSpace($KeyAlg)) { $defaultKeyAlg } else { $KeyAlg }
        $ea = if ([string]::IsNullOrWhiteSpace($EncAlg)) { $defaultEncAlg } else { $EncAlg }

        switch ($PSCmdlet.ParameterSetName) {
            'Base64Url' {
                $Builder.EncryptWithSecretB64($Base64Url, $ka, $ea) | Out-Null
            }
            'HexadecimalKey' {
                $Builder.EncryptWithSecretHex($HexadecimalKey, $ka, $ea) | Out-Null
            }
            'Bytes' {
                $Builder.EncryptWithSecret($KeyBytes, $ka, $ea) | Out-Null
            }
            'PemPath' {
                $resolvedPath = Resolve-KrPath -Path $PemPath -KestrunRoot
                $Builder.EncryptWithPemPublic($resolvedPath, $ka, $ea) | Out-Null
            }
            'Certificate' {
                $Builder.EncryptWithCertificate($X509Certificate, $ka, $ea) | Out-Null
            }
            'JwkJson' {
                $Builder.EncryptWithJwkJson($JwkJson, $ka, $ea) | Out-Null
            }
            'JwkPath' {
                $resolvedPath = Resolve-KrPath -Path $JwkPath -KestrunRoot
                $Builder.EncryptWithJwkPath($resolvedPath, $ka, $ea) | Out-Null
            }
        }

        return $Builder
    }
}
