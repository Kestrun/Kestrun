<#
.SYNOPSIS
    Adds OpenAPI metadata to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiInfo cmdlet adds OpenAPI metadata such as summary, description, tags, and operation ID to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI metadata will be added.
.PARAMETER Summary
    (Optional) A brief summary of the route for OpenAPI documentation.
.PARAMETER Description
    (Optional) A detailed description of the route for OpenAPI documentation.
.PARAMETER Tags
    (Optional) An array of tags associated with the route for OpenAPI documentation.
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
        [string[]]$Tags,
        [Parameter(ParameterSetName = 'VerbLevel')]
        [string]$OperationId,
        [Parameter(ParameterSetName = 'VerbLevel')]
        [switch]$Deprecated,
        [Parameter(ParameterSetName = 'PathLevel')]
        [switch]$PathLevel
    )
    process {
        if ($PathLevel.IsPresent) {
            $MapRouteBuilder.PathLevelOpenAPIMetadata = [Kestrun.Hosting.Options.OpenAPICommonMetadata]::new($MapRouteBuilder.Pattern)
            if ($PsBoundParameters.ContainsKey('Summary')) {
                $MapRouteBuilder.PathLevelOpenAPIMetadata.Summary = $Summary
            }
            if ($PsBoundParameters.ContainsKey('Description')) {
                $MapRouteBuilder.PathLevelOpenAPIMetadata.Description = $Description
            }
        } else {
            if ($Verbs.Count -eq 0) {
                # Apply to all verbs defined in the MapRouteBuilder
                $Verbs = $MapRouteBuilder.HttpVerbs
            }
            if ($Verbs.Count -gt 1 -and $PsBoundParameters.ContainsKey('OperationId')) {
                throw "OperationId cannot be set for multiple verbs."
            }
            foreach ($verb in $Verbs) {
                if (-not $MapRouteBuilder.OpenApi.ContainsKey($verb)) {
                    $MapRouteBuilder.OpenApi[$verb] = [Kestrun.Hosting.Options.OpenAPIMetadata]::new($MapRouteBuilder.Pattern)
                }
                $MapRouteBuilder.OpenApi[$verb].Deprecated = $Deprecated.IsPresent
                if ($PsBoundParameters.ContainsKey('Summary')) {
                    $MapRouteBuilder.OpenApi[$verb].Summary = $Summary
                }
                if ($PsBoundParameters.ContainsKey('Description')) {
                    $MapRouteBuilder.OpenApi[$verb].Description = $Description
                }
                if ($PsBoundParameters.ContainsKey('Tags')) {
                    $MapRouteBuilder.OpenApi[$verb].Tags = $Tags
                }
                if ($PsBoundParameters.ContainsKey('OperationId')) {
                    $MapRouteBuilder.OpenApi[$verb].OperationId = $OperationId
                }
            }
        }
        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
