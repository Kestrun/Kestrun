<#
.SYNOPSIS
    Adds an OpenAPI response to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiResponse cmdlet adds an OpenAPI response with a specified status code and description to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI response will be added.
.PARAMETER Verbs
    An array of HTTP verbs (e.g., GET, POST) to which the OpenAPI response will be applied. If not specified, the response will be applied to all verbs defined in the Map Route Builder.
.PARAMETER StatusCode
    The HTTP status code for the OpenAPI response.
.PARAMETER Description
    A description of the OpenAPI response.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items' |
    Add-KrMapRouteOpenApiResponse -StatusCode '200' -Description 'Successful response'|
    Add-KrMapRouteOpenApiResponse -StatusCode '404' -Description 'Item not found'
 .NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteOpenApiResponse {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter()]
        [Kestrun.Utilities.HttpVerb[]]$Verbs,
        [Parameter(Mandatory = $true)]
        [string]$StatusCode,
        [Parameter(Mandatory = $true)]
        [string]$Description
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
            $MapRouteBuilder.OpenApi[$verb].Responses[$StatusCode] = [Microsoft.OpenApi.IOpenApiResponse]::new()
            #@{
            #       Description = $Description
            #    }

            # Return the modified MapRouteBuilder for pipeline chaining
            return $MapRouteBuilder
        }
    }
}
