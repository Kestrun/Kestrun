<#
    .SYNOPSIS
        Validates a JWT token against the builder's parameters.
    .DESCRIPTION
        This function validates a JWT token against the parameters set in the JWT builder, checking for expiration, issuer, audience, and other claims.
    .PARAMETER Result
        The JWT builder result containing the token and validation parameters.
    .PARAMETER Token
        The JWT token to validate.
    .PARAMETER ClockSkew
        The allowed clock skew for validation, defaulting to 1 minute.
    .OUTPUTS
        [bool]
        Returns true if the token is valid, otherwise false.
    .EXAMPLE
        $isValid = New-KrJWTTokenBuilder | Add-KrJWTSubject -Subject "mySubject" | Build-KrJWT | Test-KrJWT -Token $token
        This example creates a new JWT token builder, adds a subject, and then tests the validity of the JWT token.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to validate JWT tokens.
        Maps to JwtBuilderResult.Validate
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
#>
function Test-KrJWT {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Jwt.JwtBuilderResult] $Result,
        [Parameter(Mandatory)]
        [string] $Token,
        [Parameter()]
        [TimeSpan] $ClockSkew = ([TimeSpan]::FromMinutes(1))
    )
    process {
        $validationResult = $Result.Validate($Token, $ClockSkew)
        return $validationResult.IsValid
    }
}
