function Limit-KrJWTNotBefore {
    <#
    .SYNOPSIS
        Sets the NotBefore time for the JWT token builder.
    .DESCRIPTION
        This function sets the NotBefore time for the JWT token builder, specifying when the token becomes valid.
    .PARAMETER Builder
        The JWT token builder to modify.
    .PARAMETER UtcBefore
        The UTC time before which the JWT token is not valid.
    .OUTPUTS
        [Kestrun.Security.JwtTokenBuilder]
        The modified JWT token builder.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Limit-KrJWTNotBefore -UtcBefore (Get-Date).AddMinutes(-5)
        This example creates a new JWT token builder and sets its NotBefore time to 5 minutes in the past.
    .NOTES
        This function is part of the Kestrun.Security module and is used to build JWT tokens
        Maps to JwtTokenBuilder.NotBefore
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken?view=azure-dotnet
    #>
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Kestrun.Security.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Security.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [DateTime] $UtcBefore
    )
    process {
        return $Builder.NotBefore($UtcBefore)
    }
}