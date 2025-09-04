
<#
    .SYNOPSIS
        Adds a claim to the JWT token builder.
    .DESCRIPTION
        This function adds a claim to the JWT token builder, allowing for the specification of additional data.
    .PARAMETER Builder
        The JWT token builder to modify.
    .PARAMETER ClaimType
        The type of the claim to add.
    .PARAMETER UserClaimType
        The user-specific type of the claim to add, such as "Admin", "User", etc.
        This parameter is used when the claim type is based on user identity claims.
    .PARAMETER Value
        The value of the claim to add.
    .OUTPUTS
        [Kestrun.Jwt.JwtTokenBuilder]
        The modified JWT token builder.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Add-KrJWTClaim -Type "role" -Value "admin"
        This example creates a new JWT token builder and adds a claim to it.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to build JWT tokens
        Maps to JwtTokenBuilder.AddClaim
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
#>
function Add-KrJWTClaim {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'ClaimType')]
    [OutputType([Kestrun.Jwt.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Jwt.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory = $true, ParameterSetName = 'ClaimType')]
        [string] $ClaimType,
        [Parameter(Mandatory = $true, ParameterSetName = 'UserClaimType')]
        [Kestrun.Claims.UserIdentityClaim] $UserClaimType,
        [Parameter(Mandatory = $true)]
        [string] $Value
    )
    process {
        # resolve ClaimType if the user chose the enum parameter-set
        if ($UserClaimType) {
            $ClaimType = [Kestrun.Claims.KestrunClaimExtensions]::ToClaimUri($UserClaimType)
        }

        return $Builder.AddClaim($ClaimType, $Value)
    }
}
