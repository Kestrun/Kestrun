<#
    .SYNOPSIS
        Adds an issuer to the JWT token builder.
    .DESCRIPTION
        This function adds an issuer to the JWT token builder, allowing for the specification of the token's issuer.
    .PARAMETER Builder
        The JWT token builder to modify.
    .PARAMETER Issuer
        The issuer to set for the JWT token.
    .OUTPUTS
        [Kestrun.Jwt.JwtTokenBuilder]
        The modified JWT token builder.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Add-KrJWTIssuer -Issuer "myIssuer"
        This example creates a new JWT token builder and adds an issuer to it.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to build JWT tokens
        Maps to JwtTokenBuilder.WithIssuer
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
#>
function Add-KrJWTIssuer {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Kestrun.Jwt.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Jwt.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $Issuer
    )
    process {
        return $Builder.WithIssuer($Issuer)
    }
}

