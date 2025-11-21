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
.PARAMETER ReferenceId
    A reference string for the OpenAPI response.
.PARAMETER Embed
    A switch indicating whether to embed the response definition directly into the route or to reference it.
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
    [CmdletBinding(defaultParameterSetName = 'DescriptionOnly')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter()]
        [Kestrun.Utilities.HttpVerb[]]$Verbs,
        [Parameter()]
        [string]$DocId = [Kestrun.Authentication.IOpenApiAuthenticationOptions]::DefaultSchemeName,
        [Parameter(Mandatory = $true)]
        [string]$StatusCode,
        [Parameter(ParameterSetName = 'Reference')]
        [Parameter(Mandatory = $true, ParameterSetName = 'DescriptionOnly')]
        [string]$Description,
        [Parameter(ParameterSetName = 'Reference')]
        [string]$ReferenceId,
        [Parameter(ParameterSetName = 'Reference')]
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
            if ($PSBoundParameters.ContainsKey('ReferenceId')) {
                # Use reference-based response
                $responses = $MapRouteBuilder.Server.OpenApiDocumentDescriptor[$DocId].Document.Components.Responses
                if (-not $responses.ContainsKey($ReferenceId)) {
                    throw "Response with ReferenceId '$ReferenceId' does not exist in the OpenAPI document components."
                }
                if ($Embed) {
                    $resp = [Kestrun.OpenApi.OpenApiComponentClone]::Clone($responses[$ReferenceId])
                    if ($PSBoundParameters.ContainsKey('Description')) {
                        $resp.Description = $Description
                    }
                    $response = $resp
                } else {
                    $response = [Microsoft.OpenApi.OpenApiResponseReference]::new($ReferenceId)
                }
            } else {
                # Create a new response with description only
                $response = [Microsoft.OpenApi.OpenApiResponse]::new()
                $response.Description = $Description
            }

            # Ensure the MapRouteBuilder is marked as having OpenAPI metadata
            $MapRouteBuilder.OpenApi[$verb].Enabled = $true
            # Add description if provided
            if ($PSBoundParameters.ContainsKey('Description')) {
                $response.Description = $Description
            }
            # Add the response to the MapRouteBuilder
            $MapRouteBuilder.OpenApi[$verb].Responses[$StatusCode] = $response
        }
        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
