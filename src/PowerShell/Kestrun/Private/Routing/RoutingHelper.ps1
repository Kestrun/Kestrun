[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '')]
param()

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
function _KrJoin-Route([string]$base, [string]$child) {
    $b = ($base ?? '').TrimEnd('/')
    $c = ($child ?? '').TrimStart('/')
    if ([string]::IsNullOrWhiteSpace($b)) { "/$c".TrimEnd('/') -replace '^$', '/' }
    elseif ([string]::IsNullOrWhiteSpace($c)) { if ($b) { $b } else { '/' } }
    else { "$b/$c" }
}


<#
    .SYNOPSIS
        Merges two arrays, preserving unique values.
    .DESCRIPTION
        This function takes two arrays and merges them into a single array,
        preserving only unique values.
    .PARAMETER a
        The first array to merge.
    .PARAMETER b
        The second array to merge.
    .OUTPUTS
        Array
#>
function _KrMerge-Unique([string[]]$a, [string[]]$b) {
    @(($a + $b | Where-Object { $_ -ne $null } | Select-Object -Unique))
}

<#
    .SYNOPSIS
        Merges two hashtables.
    .DESCRIPTION
        This function takes two hashtables and merges them into a single hashtable.
        If a key exists in both hashtables, the value from the second hashtable will be used.
    .PARAMETER a
        The first hashtable to merge.
    .PARAMETER b
        The second hashtable to merge.
    .OUTPUTS
        Hashtable
#>
function _KrMerge-Args([hashtable]$a, [hashtable]$b) {
    if (-not $a) { return $b }
    if (-not $b) { return $a }
    $m = @{}
    foreach ($k in $a.Keys) { $m[$k] = $a[$k] }
    foreach ($k in $b.Keys) { $m[$k] = $b[$k] } # child overrides
    $m
}

<#
    .SYNOPSIS
        Creates a new MapRouteOptions object with the specified base and overrides.
    .DESCRIPTION
        This function takes an existing MapRouteOptions object and a hashtable of overrides,
        and returns a new MapRouteOptions object with the merged properties.
        The merged properties will prioritize the values from the Override hashtable.
    .PARAMETER Base
        The base MapRouteOptions object to use as a template.
        This object will be cloned, and the properties will be merged with the Override hashtable.
    .PARAMETER Override
        A hashtable of properties to override in the base MapRouteOptions object.
        Any properties not specified in the Override hashtable will retain their original values from the Base object.
    .OUTPUTS
        Kestrun.Hosting.Options.MapRouteOptions
#>
function _KrWith-MRO {
    param(
        [Parameter(Mandatory)][Kestrun.Hosting.Options.MapRouteOptions]$Base,
        [Parameter()][hashtable]$Override = @{}
    )
    $h = @{
        Pattern = $Base.Pattern
        HttpVerbs = $Base.HttpVerbs
        Code = $Base.Code
        Language = $Base.Language
        ExtraImports = $Base.ExtraImports
        ExtraRefs = $Base.ExtraRefs
        RequireSchemes = $Base.RequireSchemes
        RequirePolicies = $Base.RequirePolicies
        CorsPolicyName = $Base.CorsPolicyName
        Arguments = $Base.Arguments
        OpenAPI = $Base.OpenAPI
        ThrowOnDuplicate = $Base.ThrowOnDuplicate
    }
    foreach ($k in $Override.Keys) { $h[$k] = $Override[$k] }
    return New-KrMapRouteOption -Property $h
}

<#
    .SYNOPSIS
        Merges two MapRouteOptions objects.
    .DESCRIPTION
        This function takes two MapRouteOptions objects and merges them into a single object.
        The properties from the parent object will be preserved, and the properties from the child
        object will override any matching properties in the parent object.
    .PARAMETER Parent
        The parent MapRouteOptions object.
    .PARAMETER Child
        The child MapRouteOptions object.
    .OUTPUTS
        Kestrun.Hosting.Options.MapRouteOptions
#>
function _KrMerge-MRO {
    param(
        [Parameter(Mandatory)][Kestrun.Hosting.Options.MapRouteOptions]$Parent,
        [Parameter(Mandatory)][Kestrun.Hosting.Options.MapRouteOptions]$Child
    )
    $pattern = if ($Child.Pattern) {
        if ($Parent.Pattern) { "$($Parent.Pattern)/$($Child.Pattern)" } else { $Child.Pattern }
    } else { $Parent.Pattern }

    $extraRefs = if ($null -ne $Child.ExtraRefs) {
        if ($Parent.ExtraRefs) {
            $Parent.ExtraRefs + $Child.ExtraRefs
        } else {
            $Child.ExtraRefs
        }
    } else { $Parent.ExtraRefs }

    $merged = @{
        Pattern = $pattern.Replace('//', '/')
        HttpVerbs = if ($null -ne $Child.HttpVerbs -and ($Child.HttpVerbs.Count -gt 0)) { $Child.HttpVerbs } else { $Parent.HttpVerbs }
        Code = if ($Child.Code) { $Child.Code } else { $Parent.Code }
        Language = if ($null -ne $Child.Language) { $Child.Language } else { $Parent.Language }
        ExtraImports = _KrMerge-Unique $Parent.ExtraImports $Child.ExtraImports
        ExtraRefs = $extraRefs
        RequireSchemes = _KrMerge-Unique $Parent.RequireSchemes $Child.RequireSchemes
        RequirePolicies = _KrMerge-Unique $Parent.RequirePolicies $Child.RequirePolicies
        CorsPolicyName = if ($Child.CorsPolicyName) { $Child.CorsPolicyName } else { $Parent.CorsPolicyName }
        Arguments = _KrMerge-Args $Parent.Arguments $Child.Arguments
        OpenAPI = if ($Child.OpenAPI) { $Child.OpenAPI } else { $Parent.OpenAPI }
        ThrowOnDuplicate = $Child.ThrowOnDuplicate -or $Parent.ThrowOnDuplicate
    }
    return New-KrMapRouteOption -Property $merged
}
