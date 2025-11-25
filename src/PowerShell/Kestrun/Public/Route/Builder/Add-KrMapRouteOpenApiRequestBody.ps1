<#
.SYNOPSIS
    Adds an OpenAPI request body to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiRequestBody cmdlet adds an OpenAPI request body with an optional description and ReferenceId to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI request body will be added.
.PARAMETER Verbs
    An array of HTTP verbs (e.g., GET, POST) to which the OpenAPI request body will be applied. If not specified, the request body will be applied to all verbs defined in the Map Route Builder.
.PARAMETER DocId
    The documentation ID associated with the OpenAPI request body. Default is the default authentication scheme name
.PARAMETER Description
    A description of the OpenAPI request body.
.PARAMETER ReferenceId
    A ReferenceId string for the OpenAPI request body.
.PARAMETER Force
    A switch to force adding a request body to HTTP verbs that typically do not support it (e.g., GET, HEAD, TRACE).
.PARAMETER Embed
    A switch indicating whether to embed the request body definition directly into the route or to reference it
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('POST') -Pattern '/api/items' |
    Add-KrMapRouteOpenApiRequestBody -Description 'Item creation request body' -ReferenceId 'ItemCreateRequestBody'
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteOpenApiRequestBody {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter()]
        [Kestrun.Utilities.HttpVerb[]]$Verbs,
        [Parameter()]
        [string]$DocId = [Kestrun.Authentication.IOpenApiAuthenticationOptions]::DefaultSchemeName,
        [Parameter()]
        [string]$Description,
        [Parameter(Mandatory = $true)]
        [string]$ReferenceId,
        [Parameter()]
        [switch]$Force,
        [Parameter()]
        [switch]$Embed
    )
    process {
        return $MapRouteBuilder.AddOpenApiRequestBody(
            $ReferenceId,
            $Verbs,
            $DocId,
            $Description,
            $Force.IsPresent,
            $Embed.IsPresent
        )
    }
}
