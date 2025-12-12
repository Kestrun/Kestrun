<#
.SYNOPSIS
    Sets the CORS origin for a given CorsPolicyBuilder object.
.DESCRIPTION
    This function sets the CORS origin for a given CorsPolicyBuilder object. It supports setting the origin to any value, a specific set of origins, or allowing wildcard subdomains.
.PARAMETER Builder
    The CorsPolicyBuilder object to set the CORS origin for.
.PARAMETER Any
    If specified, sets the CORS origin to any value.
.PARAMETER Origins
    The specific set of origins to allow.
.PARAMETER AllowWildcardSubdomains
    If specified, allows wildcard subdomains for the CORS origin.
.EXAMPLE
    New-KrCorsPolicyBuilder | Set-KrCorsOrigin -Builder $corsPolicyBuilder -Any
    Sets the CORS origin to any value.
.EXAMPLE
    New-KrCorsPolicyBuilder | Set-KrCorsOrigin -Builder $corsPolicyBuilder -Origins @('http://example.com', 'https://example.com')
    Sets the CORS origin to a specific set of origins.
#>
function Set-KrCorsOrigin {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding(DefaultParameterSetName = 'With')]
    [OutputType([Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder]$Builder,

        [Parameter(Mandatory, ParameterSetName = 'Any')]
        [switch]$Any,

        [Parameter(Mandatory, ParameterSetName = 'With')]
        [string[]]$Origins,

        [Parameter(ParameterSetName = 'With')]
        [switch]$AllowWildcardSubdomains
    )
    process {
        if ($Any) { $Builder.AllowAnyOrigin() | Out-Null }
        else { $Builder.WithOrigins($Origins) | Out-Null }

        if ($AllowWildcardSubdomains) {
            $Builder.SetIsOriginAllowedToAllowWildcardSubdomains() | Out-Null
        }

        $Builder
    }
}
