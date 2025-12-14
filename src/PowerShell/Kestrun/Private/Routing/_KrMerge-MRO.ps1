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

    $extraRefs = if ($null -ne $Child.ScriptCode.ExtraRefs) {
        if ($Parent.ScriptCode.ExtraRefs) {
            $Parent.ScriptCode.ExtraRefs + $Child.ScriptCode.ExtraRefs
        } else {
            $Child.ScriptCode.ExtraRefs
        }
    } else { $Parent.ScriptCode.ExtraRefs }

    $merged = @{
        Pattern = $pattern.Replace('//', '/')
        HttpVerbs = if ($null -ne $Child.HttpVerbs -and ($Child.HttpVerbs.Count -gt 0)) { $Child.HttpVerbs } else { $Parent.HttpVerbs }
        RequireSchemes = _KrMerge-Unique $Parent.RequireSchemes $Child.RequireSchemes
        RequirePolicies = _KrMerge-Unique $Parent.RequirePolicies $Child.RequirePolicies
        CorsPolicy = if ($Child.CorsPolicy) { $Child.CorsPolicy } else { $Parent.CorsPolicy }
        OpenAPI = if ($Child.OpenAPI) { $Child.OpenAPI } else { $Parent.OpenAPI }
        ThrowOnDuplicate = $Child.ThrowOnDuplicate -or $Parent.ThrowOnDuplicate
        ScriptCode = @{
            Code = if ($Child.ScriptCode.Code) { $Child.ScriptCode.Code } else { $Parent.ScriptCode.Code }
            Language = if ($null -ne $Child.ScriptCode.Language) { $Child.ScriptCode.Language } else { $Parent.ScriptCode.Language }
            ExtraImports = _KrMerge-Unique $Parent.ScriptCode.ExtraImports $Child.ScriptCode.ExtraImports
            ExtraRefs = $extraRefs
            Arguments = _KrMerge-Args $Parent.ScriptCode.Arguments $Child.ScriptCode.Arguments
        }
    }
    return New-KrMapRouteOption -Property $merged
}

