<#
    .SYNOPSIS
        Retrieves information from a JWT token.
    .DESCRIPTION
        This function extracts various parameters from a JWT token, such as issuer, audience, expiration, and claims.
    .PARAMETER Token
        The JWT token to inspect.
    .OUTPUTS
        [Kestrun.Jwt.JwtParameters]
        An object containing the extracted parameters from the JWT token.
    .EXAMPLE
        $jwtInfo = Get-KrJWTInfo -Token $token
        This example retrieves the information from the specified JWT token.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to inspect JWT tokens.
        Maps to JwtInspector.ReadAllParameters
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
#>
function Get-KrJWTInfo {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Kestrun.Jwt.JwtParameters])]
    param(
        [Parameter(Mandatory)]
        [string] $Token
    )
    process {
        return [Kestrun.Jwt.JwtInspector]::ReadAllParameters($Token)
    }
}

