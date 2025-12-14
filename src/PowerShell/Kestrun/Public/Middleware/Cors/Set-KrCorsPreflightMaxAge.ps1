<#
.SYNOPSIS
    Sets the preflight max age for CORS policies in ASP.NET Core.
.DESCRIPTION
    This function sets the preflight max age for CORS policies in ASP.NET Core.
    It takes a CorsPolicyBuilder object and a TimeSpan object as input parameters.
    The SetPreflightMaxAge method of the CorsPolicyBuilder object is called with the provided TimeSpan object to set the preflight max age.
    The modified CorsPolicyBuilder object is then returned.
.PARAMETER Builder
    The CorsPolicyBuilder object to set the preflight max age for.
.PARAMETER MaxAge
    The TimeSpan object representing the preflight max age to set.
.PARAMETER Seconds
    The number of seconds representing the preflight max age to set.
.EXAMPLE
    New-KrCorsPolicyBuilder | Set-KrCorsPreflightMaxAge -MaxAge (New-TimeSpan -Hours 24) | Add-KrCorsPolicy -Server $server -Name 'MyCORSPolicy'
.EXAMPLE
    New-KrCorsPolicyBuilder | Set-KrCorsPreflightMaxAge -Seconds 86400 | Add-KrCorsPolicy -Name 'MyCORSPolicy'
.OUTPUTS
    Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder
#>
function Set-KrCorsPreflightMaxAge {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding(DefaultParameterSetName = 'TimeSpan')]
    [OutputType([Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder]$Builder,

        [Parameter(Mandatory = $true, ParameterSetName = 'TimeSpan')]
        [TimeSpan]$MaxAge,

        [Parameter(Mandatory = $true, ParameterSetName = 'Seconds')]
        [int]$Seconds
    )
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Seconds') {
            $MaxAge = New-TimeSpan -Seconds $Seconds
        }
        $Builder.SetPreflightMaxAge($MaxAge) | Out-Null
        $Builder
    }
}
