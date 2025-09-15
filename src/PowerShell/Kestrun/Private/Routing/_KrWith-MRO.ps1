
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
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '')]
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
