<#
.SYNOPSIS
    Adds OpenAPI tags to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiTag cmdlet adds OpenAPI tags to a Map Route Builder object for specified HTTP verbs.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI tags will be added.
.PARAMETER Verbs
    An array of HTTP verbs (e.g., GET, POST) to which the OpenAPI tags will be applied. If not specified, the tags will be applied to all verbs defined in the Map Route Builder.
.PARAMETER Tag
    The OpenAPI tag to be added to the Map Route Builder.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder -Verbs @('GET') -Pattern '/api/items' |
    Add-KrMapRouteOpenApiTag -Tag 'Items'
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteOpenApiTag {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter()]
        [Kestrun.Utilities.HttpVerb[]]$Verbs,
        [Parameter(Mandatory = $true)]
        [string]$Tag
    )
    process {
        if ($Verbs.Count -eq 0) {
            # Apply to all verbs defined in the MapRouteBuilder
            $Verbs = $MapRouteBuilder.HttpVerbs
        }
        foreach ($verb in $Verbs) {
            if (-not $MapRouteBuilder.OpenApi.ContainsKey($verb)) {
                $MapRouteBuilder.OpenApi[$verb] = [Kestrun.Hosting.Options.OpenAPIMetadata]::new($MapRouteBuilder.Pattern)
            }
            $MapRouteBuilder.OpenApi[$verb].Enabled = $true
            if (-not [string]::IsNullOrEmpty($Tag)) {
                $MapRouteBuilder.OpenApi[$verb].Tags += $Tag
            }
        }
        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
