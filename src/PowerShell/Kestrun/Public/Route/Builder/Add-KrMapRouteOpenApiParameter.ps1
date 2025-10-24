<#
.SYNOPSIS
    Adds an OpenAPI parameter to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiParameter cmdlet adds an OpenAPI parameter with an optional description and reference to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI parameter will be added.
.PARAMETER Verbs
    An array of HTTP verbs (e.g., GET, POST) to which the OpenAPI parameter will be applied. If not specified, the parameter will be applied to all verbs defined in the Map Route Builder.
.PARAMETER Description
    A description of the OpenAPI parameter.
.PARAMETER ReferenceId
    A reference string for the OpenAPI parameter.
.PARAMETER Embed
    A switch indicating whether to embed the parameter definition directly into the route or to reference it.
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
        [string]$Description,
        [Parameter(Mandatory = $true)]
        [string]$ReferenceId,
        [Parameter()]
        [switch]$Embed
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
            if ($Embed) {
                $parameters = $MapRouteBuilder.Server.OpenApiDocumentDescriptor['default'].Document.Components.Parameters
                if (-not $parameters.ContainsKey($ReferenceId)){
                    throw "Parameter with ReferenceId '$ReferenceId' does not exist in the OpenAPI document components."
                }
                $param = [Kestrun.OpenApi.OpenApiComponentClone]::Clone($parameters[$ReferenceId])
                if ($PSBoundParameters.ContainsKey('Description')) {
                    $param.Description = $Description
                }
                $MapRouteBuilder.OpenApi[$verb].Parameters.Add($param)
            } else {
                $param = [Microsoft.OpenApi.OpenApiParameterReference]::new($ReferenceId)
                if ($PSBoundParameters.ContainsKey('Description')) {
                    $param.Description = $Description
                }
                $MapRouteBuilder.OpenApi[$verb].Parameters.Add($param)
            }
        }
        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
