<#
.SYNOPSIS
    Adds OpenAPI tags to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiTag cmdlet adds OpenAPI tags to a Map Route Builder object for specified HTTP verbs.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI tags will be added.
.PARAMETER Verbs
    An array of HTTP verbs (e.g., GET, POST) to which the OpenAPI tags will be applied. If not specified, the tags will be applied to all verbs defined in the Map Route Builder.
.PARAMETER Tags
    The OpenAPI tags to be added to the Map Route Builder.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder -Verbs @('GET') -Pattern '/api/items' |
    Add-KrMapRouteOpenApiTag -Tags 'Items'
    This example creates a new Map Route Builder and adds the OpenAPI tag 'Items' to the GET verb.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items' |
    Add-KrMapRouteOpenApiTag -Verbs @('POST') -Tags 'CreateItem'
    This example creates a new Map Route Builder and adds the OpenAPI tag 'CreateItem' only to the POST verb.
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
        [string[]]$Tags
    )
    process {
        return $MapRouteBuilder.AddOpenApiTag($Tags, $Verbs)
    }
}
