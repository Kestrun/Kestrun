<#
.SYNOPSIS
    Adds OpenAPI metadata to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiInfo cmdlet adds OpenAPI metadata such as summary, description, tags, and operation ID to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI metadata will be added.
.PARAMETER Verbs
    An array of HTTP verbs to which the OpenAPI metadata will be applied.
    If not specified, the metadata will be applied to all verbs defined in the Map Route Builder
.PARAMETER Summary
    (Optional) A brief summary of the route for OpenAPI documentation.
.PARAMETER Description
    (Optional) A detailed description of the route for OpenAPI documentation.
.PARAMETER OperationId
    (Optional) A unique operation ID for the route in OpenAPI documentation.
.PARAMETER Deprecated
    (Optional) Indicates whether the route is deprecated in OpenAPI documentation.
.PARAMETER PathLevel
    (Optional) If specified, applies the OpenAPI metadata at the path level instead of the verb level.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder -Pattern '/api/items' -Verbs @('GET', 'POST') |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items' |
    Add-KrMapRouteOpenApiInfo -Summary 'Get Items' -Description 'Retrieves a list of items.' -Tags @('Items', 'API') -OperationId 'GetItems' -Deprecated
.EXAMPLE
    # Add path-level OpenAPI metadata
    $mapRouteBuilder = New-KrMapRouteBuilder -Pattern '/api/items' -Verbs @('GET', 'POST') |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items' |
    Add-KrMapRouteOpenApiInfo -Summary 'Items API' -Description 'Operations related to items.' -PathLevel
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteOpenApiInfo {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    [CmdletBinding(defaultParameterSetName = 'VerbLevel')]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter(ParameterSetName = 'VerbLevel')]
        [Kestrun.Utilities.HttpVerb[]]$Verbs,
        [Parameter(ParameterSetName = 'PathLevel')]
        [Parameter(ParameterSetName = 'VerbLevel')]
        [string]$Summary,
        [Parameter(ParameterSetName = 'PathLevel')]
        [Parameter(ParameterSetName = 'VerbLevel')]
        [string]$Description,
        [Parameter(ParameterSetName = 'VerbLevel')]
        [string]$OperationId,
        [Parameter(ParameterSetName = 'VerbLevel')]
        [switch]$Deprecated,
        [Parameter(ParameterSetName = 'PathLevel')]
        [switch]$PathLevel
    )
    process {
        return $MapRouteBuilder.AddOpenApiInfo(
            $Summary,
            $Description,
            $OperationId,
            $Deprecated.IsPresent,
            $Verbs,
            $PathLevel.IsPresent
        )
    }
}
