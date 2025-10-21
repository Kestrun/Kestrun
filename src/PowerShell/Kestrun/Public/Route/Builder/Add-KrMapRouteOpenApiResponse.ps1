<#
.SYNOPSIS
    Adds an OpenAPI response to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiResponse cmdlet adds an OpenAPI response with a specified status code and description to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI response will be added.
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
        [Parameter(Mandatory = $true)]
        [string]$StatusCode,
        [Parameter(Mandatory = $true)]
        [string]$Description


    )
    process {
        $MapRouteBuilder.OpenApi.Enabled = $true
        $MapRouteBuilder.OpenApi.Responses[$StatusCode] =  [Microsoft.OpenApi.IOpenApiResponse]::new()
        @{
            Description = $Description
        }

        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
