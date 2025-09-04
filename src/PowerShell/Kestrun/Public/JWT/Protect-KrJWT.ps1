<#
    .SYNOPSIS
        Protects a JWT token using a specified secret or certificate.
    .DESCRIPTION
        This function allows you to sign a JWT token with a secret or certificate, ensuring its integrity and authenticity.
    .PARAMETER Builder
        The JWT token builder to modify.
    .PARAMETER Base64Url
        The Base64Url encoded secret to use for signing the JWT token.
    .PARAMETER HexadecimalKey
        The hexadecimal key to use for signing the JWT token.
    .PARAMETER Passphrase
        The passphrase to use for signing the JWT token, provided as a secure string.
    .PARAMETER PemPath
        The path to a PEM file containing the RSA key to use for signing the JWT token.
    .PARAMETER Certificate
        The X509 certificate to use for signing the JWT token.
    .PARAMETER Algorithm
        The algorithm to use for signing the JWT token.
        Defaults to 'Auto' which will determine the algorithm based on the provided secret or certificate.
    .PARAMETER X509Certificate
        The X509 certificate to use for signing the JWT token.
    .OUTPUTS
        [Kestrun.Jwt.JwtTokenBuilder]
        The modified JWT token builder with the signing configuration applied.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Protect-KrJWT -Base64Url "your_base64_url_secret"
        $builder | Protect-KrJWT -HexadecimalKey "a1b2c3d4e5f6"
        $builder | Protect-KrJWT -Passphrase (ConvertTo-SecureString "mysecret" -AsPlainText -Force)
        $builder | Protect-KrJWT -PemPath "C:\path\to\key.pem"
        $builder | Protect-KrJWT -Certificate (Get-Item "C:\path\to\certificate.pfx")
        This example demonstrates how to create a JWT token builder and apply various signing methods.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to build JWT tokens
        Maps to JwtTokenBuilder.SignWithSecret, JwtTokenBuilder.SignWithSecretHex, JwtTokenBuilder.SignWithSecretPassphrase,
        JwtTokenBuilder.SignWithRsaPem, and JwtTokenBuilder.SignWithCertificate methods.
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
#>
function Protect-KrJWT {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'SecretBase64Url')]
    [OutputType([Kestrun.Jwt.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Jwt.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory = $true, ParameterSetName = 'SecretBase64Url')]
        [string] $Base64Url,
        [Parameter(Mandatory = $true, ParameterSetName = 'SecretHexadecimalKey')]
        [string] $HexadecimalKey,
        [Parameter(Mandatory = $true, ParameterSetName = 'SecretPassphrase')]
        [securestring] $Passphrase,
        [Parameter(Mandatory = $true, ParameterSetName = 'PemPath')]
        [string] $PemPath,
        [Parameter(Mandatory = $true, ParameterSetName = 'Certificate')]
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $X509Certificate,
        [Parameter(Mandatory = $false)]
        [ValidateSet('Auto', 'HS256', 'HS384', 'HS512', 'RS256', 'RS384', 'RS512', 'ES256', 'ES384', 'ES512')]
        [string] $Algorithm = 'Auto' # Default to 'Auto' which will determine the algorithm based on the provided secret or certificate.
    )

    process {
        $algEnum = [Kestrun.Jwt.JwtAlgorithm]::$Algorithm
        switch ($PSCmdlet.ParameterSetName) {
            'SecretBase64Url' {
                $Builder.SignWithSecret($Base64Url, $algEnum) | Out-Null
                break
            }
            'SecretHexadecimalKey' {
                $Builder.SignWithSecretHex($HexadecimalKey, $algEnum) | Out-Null
                break
            }
            'SecretPassphrase' {
                $Builder.SignWithSecretPassphrase($Passphrase, $algEnum) | Out-Null
                break
            }
            'PemPath' {
                $resolvedPath = Resolve-KrPath -Path $PemPath -KestrunRoot
                $Builder.SignWithRsaPem($resolvedPath, $algEnum) | Out-Null
                break
            }
            'Certificate' {
                $Builder.SignWithCertificate($X509Certificate, $algEnum) | Out-Null
                break
            }
        }
        return $Builder
    }
}
