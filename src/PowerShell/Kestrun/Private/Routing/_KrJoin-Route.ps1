<#
    .SYNOPSIS
        Joins two route paths.
    .DESCRIPTION
        This function takes a base route and a child route, and joins them into a single route.
    .PARAMETER base
        The base route to use.
    .PARAMETER child
        The child route to join with the base route.
    .OUTPUTS
        String
#>
function _KrJoin-Route {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '')]
    param([string]$base, [string]$child)
    $b = ($base ?? '').TrimEnd('/')
    $c = ($child ?? '').TrimStart('/')
    if ([string]::IsNullOrWhiteSpace($b)) { "/$c".TrimEnd('/') -replace '^$', '/' }
    elseif ([string]::IsNullOrWhiteSpace($c)) { if ($b) { $b } else { '/' } }
    else { "$b/$c" }
}
