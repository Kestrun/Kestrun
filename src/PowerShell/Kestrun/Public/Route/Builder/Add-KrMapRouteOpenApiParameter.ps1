<#
.SYNOPSIS
    Adds an OpenAPI parameter to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiParameter cmdlet adds an OpenAPI parameter with an optional description and reference to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI parameter will be added.
.PARAMETER Verbs
    An array of HTTP verbs (e.g., GET, POST) to which the OpenAPI parameter will be applied. If not specified, the parameter will be applied to all verbs defined in the Map Route Builder.
.PARAMETER DocId
    The documentation ID associated with the OpenAPI parameter. Default is the default authentication scheme name.
.PARAMETER Description
    A description of the OpenAPI parameter.
.PARAMETER ReferenceId
    A reference string for the OpenAPI parameter.
.PARAMETER Embed
    A switch indicating whether to embed the parameter definition directly into the route or to reference it.
.PARAMETER Key
    An optional key to set the name of the parameter in the OpenAPI definition.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET') -Pattern '/api/items' |
    Add-KrMapRouteOpenApiParameter -Description 'Item ID parameter' -ReferenceId 'ItemIdParam'
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteOpenApiParameter {
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
        [switch]$Embed,
        [Parameter()]
        [string]$Key
    )
    process {
        return $MapRouteBuilder.AddOpenApiParameter(
            $ReferenceId, $Verbs, $DocId, $Description, $Embed.IsPresent, $Key)
    }
}
