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
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '')]
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

