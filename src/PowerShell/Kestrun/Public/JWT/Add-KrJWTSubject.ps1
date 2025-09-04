<#
    .SYNOPSIS
        Adds a subject to the JWT token builder.
    .DESCRIPTION
        This function adds a subject to the JWT token builder, allowing for the specification of the token's subject.
    .PARAMETER Builder
        The JWT token builder to modify.
    .PARAMETER Subject
        The subject to set for the JWT token.
    .OUTPUTS
        [Kestrun.Jwt.JwtTokenBuilder]
        The modified JWT token builder.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Add-KrJWTSubject -Subject "mySubject"
        This example creates a new JWT token builder and adds a subject to it.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to build JWT tokens
        Maps to JwtTokenBuilder.WithSubject
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
#>
function Add-KrJWTSubject {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Kestrun.Jwt.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Jwt.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $Subject
    )
    process {
        return $Builder.WithSubject($Subject)
    }
}

