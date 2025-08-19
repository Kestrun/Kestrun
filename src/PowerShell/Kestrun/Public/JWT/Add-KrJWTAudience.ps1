<#
    .SYNOPSIS
        Adds an audience to the JWT token builder.
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
    .DESCRIPTION
        This function adds an audience to the JWT token builder, allowing for the specification of the token's audience.
    .PARAMETER Builder
        The JWT token builder to modify.
    .PARAMETER Audience
        The audience to set for the JWT token.
    .OUTPUTS
        [Kestrun.Jwt.JwtTokenBuilder]
        The modified JWT token builder.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Add-KrJWTAudience -Audience "myAudience"
        This example creates a new JWT token builder and adds an audience to it.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to build JWT tokens
        Maps to JwtTokenBuilder.WithAudience
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
#>
function Add-KrJWTAudience {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Kestrun.Jwt.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Jwt.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $Audience
    )
    process {
        return $Builder.WithAudience($Audience)
    }
}
