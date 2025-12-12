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
.EXAMPLE
    New-KrCorsPolicyBuilder | Set-KrCorsPreflightMaxAge -MaxAge (New-TimeSpan -Hours 24)
.OUTPUTS
    Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder
#>
function Set-KrCorsPreflightMaxAge {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder]$Builder,

        [Parameter(Mandatory)]
        [TimeSpan]$MaxAge
    )
    process {
        $Builder.SetPreflightMaxAge($MaxAge) | Out-Null
        $Builder
    }
}
