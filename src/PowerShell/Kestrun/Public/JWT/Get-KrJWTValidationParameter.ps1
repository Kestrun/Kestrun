<#
    .SYNOPSIS
        Retrieves the validation parameters for a JWT token builder result.
    .DESCRIPTION
        This function extracts the validation parameters from a JWT builder result, which can be used for validating JWT tokens.
    .PARAMETER Result
        The JWT builder result containing the token to validate.
    .PARAMETER ClockSkew
        The allowed clock skew for validation, defaulting to 1 minute.
    .OUTPUTS
        [System.IdentityModel.Tokens.Jwt.TokenValidationParameters]
        The validation parameters extracted from the JWT builder result.
    .EXAMPLE
        $validationParams = Get-KrJWTValidationParameter -Result $tokenBuilderResult -ClockSkew (New-TimeSpan -Minutes 5)
        This example retrieves the validation parameters from the specified JWT builder result with a clock skew of 5 minutes.
    .EXAMPLE
        $JwtKeyHex = "6f1a1ce2e8cc4a5685ad0e1d1f0b8c092b6dce4f7a08b1c2d3e4f5a6b7c8d9e0";
        $JwtTokenBuilder = New-KrJWTBuilder |
        Add-KrJWTIssuer    -Issuer   $issuer |
        Add-KrJWTAudience  -Audience $audience |
        Protect-KrJWT -HexadecimalKey $JwtKeyHex -Algorithm HS256

        # Add a JWT bearer authentication scheme using the validation parameters
        Add-KrJWTBearerAuthentication -AuthenticationScheme "JwtScheme" -Options (Build-KrJWT -Builder $JwtTokenBuilder | Get-KrJWTValidation)
        This example creates a JWT token builder, adds an issuer and audience, protects the JWT with a hexadecimal key, and retrieves the validation parameters for use in authentication.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to manage JWT tokens.
        Maps to JwtBuilderResult.GetValidationParameters
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
#>
function Get-KrJWTValidationParameter {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Jwt.JwtBuilderResult] $Result,
        [Parameter()]
        [TimeSpan] $ClockSkew = ([TimeSpan]::FromMinutes(1))
    )
    process {
        return $Result.GetValidationParameters($ClockSkew)
    }
}

