<#
    .SYNOPSIS
        Retrieves the JWT token from the builder result.
    .DESCRIPTION
        This function extracts the JWT token from the builder result, allowing for further processing or output.
    .PARAMETER Result
        The JWT builder result containing the constructed token.
    .OUTPUTS
        [string]
        The JWT token extracted from the builder result.
    .EXAMPLE
        $token = New-KrJWTTokenBuilder | Add-KrJWTSubject -Subject "mySubject" | Build-KrJWT |
                  Get-KrJWTToken
        This example creates a new JWT token builder, adds a subject, builds the JWT token, and then retrieves the token.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to retrieve JWT tokens.
        Maps to JwtBuilderResult.Token
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
#>
function Get-KrJWTToken {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Jwt.JwtBuilderResult] $Result
    )
    process {
        return $Result.Token()
    }
}

