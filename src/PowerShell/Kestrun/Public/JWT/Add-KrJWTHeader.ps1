<#
    .SYNOPSIS
        Adds a header to the JWT token builder.
    .DESCRIPTION
        This function adds a header to the JWT token builder, allowing for the specification of additional header fields.
    .PARAMETER Builder
        The JWT token builder to modify.
    .PARAMETER Name
        The name of the header to add.
    .PARAMETER Value
        The value of the header to add.
    .OUTPUTS
        [Kestrun.Jwt.JwtTokenBuilder]
        The modified JWT token builder.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Add-KrJWTHeader -Name "customHeader" -Value "headerValue"
        This example creates a new JWT token builder and adds a custom header to it.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to build JWT tokens.
        Maps to JwtTokenBuilder.AddHeader
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
#>
function Add-KrJWTHeader {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Kestrun.Jwt.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Jwt.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory)]
        [string] $Name,
        [Parameter(Mandatory)]
        [object] $Value
    )
    process {
        return $Builder.AddHeader($Name, $Value)
    }
}

