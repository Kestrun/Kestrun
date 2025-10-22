<#
.SYNOPSIS
    Adds an OpenAPI request body to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiRequestBody cmdlet adds an OpenAPI request body with an optional description and reference to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI request body will be added.
.PARAMETER Verbs
    An array of HTTP verbs (e.g., GET, POST) to which the OpenAPI request body will be applied. If not specified, the request body will be applied to all verbs defined in the Map Route Builder.
.PARAMETER Description
    A description of the OpenAPI request body.
.PARAMETER Reference
    A reference string for the OpenAPI request body.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('POST') -Pattern '/api/items' |
    Add-KrMapRouteOpenApiRequestBody -Description 'Item creation request body' -Reference 'CreateItemBody'
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
        [string]$Description,
        [Parameter()]
        [string]$Reference
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
            $MapRouteBuilder.OpenApi[$verb].RequestBody = [Microsoft.OpenApi.OpenApiRequestBodyReference]::new($Reference)

            if ($Description) {
                $MapRouteBuilder.OpenApi[$verb].RequestBody.Description = $Description
            }
            # Return the modified MapRouteBuilder for pipeline chaining
            return $MapRouteBuilder
        }
    }
}
