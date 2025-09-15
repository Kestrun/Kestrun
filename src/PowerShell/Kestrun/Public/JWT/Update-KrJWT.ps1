<#
    .SYNOPSIS
        Updates an existing JWT token.
    .DESCRIPTION
        This function updates an existing JWT token by renewing it with a new lifetime.
        It can either take a token directly or extract it from the current HTTP context.
    .PARAMETER Builder
        The JWT token builder used to renew the token.
    .PARAMETER Lifetime
        The new duration for which the JWT token will be valid.
    .PARAMETER Token
        The existing JWT token to update.
    .PARAMETER FromContext
        Indicates whether to extract the token from the HTTP context.
    .OUTPUTS
        [string]
        The updated JWT token.
    .EXAMPLE
        Update-KrJWT -Builder $jwtBuilder -Token $existingToken -Lifetime (New-TimeSpan -Minutes 30)
        This updates the existing JWT token with a new lifetime of 30 minutes.
    .EXAMPLE
        Update-KrJWT -Builder $jwtBuilder -FromContext -Lifetime (New-TimeSpan -Minutes 30)
        This updates the existing JWT token extracted from the HTTP context with a new lifetime of 30 minutes.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to manage JWT tokens.
        Maps to JwtBuilderResult.Renew
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
#>
function Update-KrJWT {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding(defaultParameterSetName = 'Token')]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Jwt.JwtTokenBuilder] $Builder,
        [Parameter(Mandatory = $true, ParameterSetName = 'Token')]
        [string]$Token,
        [Parameter(Mandatory = $true, ParameterSetName = 'Context')]
        [switch] $FromContext,
        [Parameter()]
        [TimeSpan] $Lifetime
    )
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Token') {
            return $Builder.RenewJwt($Token, $Lifetime)
        }
        if ($FromContext.IsPresent -and $null -ne $Context.Request) {
            return $Builder.RenewJwt($Context, $Lifetime)
        }
    }
}

