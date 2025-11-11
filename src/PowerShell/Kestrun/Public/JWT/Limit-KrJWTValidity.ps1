<#
    .SYNOPSIS
        Sets the validity period for the JWT token.
    .DESCRIPTION
        This function sets the validity period for the JWT token, specifying how long the token will be valid.
        The period can be provided as a [TimeSpan] object (-Lifetime) or directly as -Hours, -Minutes, or -Seconds.
    .PARAMETER Builder
        The JWT token builder to modify.
    .PARAMETER Lifetime
        The duration for which the JWT token will be valid.
    .PARAMETER Hours
        The number of hours for which the JWT token will be valid.
    .PARAMETER Minutes
        The number of minutes for which the JWT token will be valid.
    .PARAMETER Seconds
        The number of seconds for which the JWT token will be valid.
    .OUTPUTS
        [Kestrun.Jwt.JwtTokenBuilder]
        The modified JWT token builder.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Limit-KrJWTValidity -Hours 1
        Creates a JWT token builder and sets its validity period to 1 hour.
    .EXAMPLE
        $builder = New-KrJWTTokenBuilder | Limit-KrJWTValidity -Lifetime (New-TimeSpan -Hours 2 -Minutes 30)
        Creates a JWT token builder and sets its validity period to 2 hours and 30 minutes.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to build JWT tokens.
        Maps to JwtTokenBuilder.ValidFor(TimeSpan).
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/system.identitymodel.tokens.jwt.jwtsecuritytoken
#>
function Limit-KrJWTValidity {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'TimeSpan')]
    [OutputType([Kestrun.Jwt.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Kestrun.Jwt.JwtTokenBuilder] $Builder,

        [Parameter(Mandatory, ParameterSetName = 'TimeSpan')]
        [TimeSpan] $Lifetime,

        [Parameter(ParameterSetName = 'Discrete')]
        [ValidateRange(0, [double]::MaxValue)]
        [double] $Hours = 0,

        [Parameter(ParameterSetName = 'Discrete')]
        [ValidateRange(0, [double]::MaxValue)]
        [double] $Minutes = 0,

        [Parameter(ParameterSetName = 'Discrete')]
        [ValidateRange(0, [double]::MaxValue)]
        [double] $Seconds = 0
    )
    process {
        $ts = ($PSCmdlet.ParameterSetName -eq 'TimeSpan') ?
        $Lifetime : [TimeSpan]::FromSeconds(($Hours * 3600) + ($Minutes * 60) + $Seconds)

        return $Builder.ValidFor($ts)
    }
}
