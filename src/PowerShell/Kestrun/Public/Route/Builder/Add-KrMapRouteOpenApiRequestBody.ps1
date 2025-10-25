<#
.SYNOPSIS
    Adds an OpenAPI request body to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiRequestBody cmdlet adds an OpenAPI request body with an optional description and ReferenceId to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI request body will be added.
.PARAMETER Verbs
    An array of HTTP verbs (e.g., GET, POST) to which the OpenAPI request body will be applied. If not specified, the request body will be applied to all verbs defined in the Map Route Builder.
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
        [Parameter(Mandatory = $true)]
        [string]$ReferenceId,
        [Parameter()]
        [switch]$Force,
        [Parameter()]
        [switch]$Embed
    )
    process {
        if ($Verbs.Count -eq 0) {
            # Apply to all verbs defined in the MapRouteBuilder
            $Verbs = $MapRouteBuilder.HttpVerbs
        }
        foreach ($verb in $Verbs) {
            if ( ((('GET', 'HEAD' ) -contains $verb) -and -not $Force.IsPresent) -or ($verb -eq 'TRACE')) {
                if (Test-KrLogger) {
                    Write-KrLog -Level Warning -Message 'Cannot add RequestBody to HTTP verb {verb} as it does not support a request body.' -Values $verb
                } else {
                    Write-Warning -Message "Cannot add RequestBody to HTTP verb [$verb] as it does not support a request body."
                }
                continue
            }
            if (-not $MapRouteBuilder.OpenApi.ContainsKey($verb)) {
                $MapRouteBuilder.OpenApi[$verb] = [Kestrun.Hosting.Options.OpenAPIMetadata]::new($MapRouteBuilder.Pattern)
            }
            if ($null -ne $MapRouteBuilder.OpenApi[$verb].RequestBody) {
                throw "RequestBody already defined for verb $verb in this MapRouteBuilder."
            }

            # Use reference-based request body
            $requestBodies = $MapRouteBuilder.Server.OpenApiDocumentDescriptor['default'].Document.Components.RequestBodies
            if (-not $requestBodies.ContainsKey($ReferenceId)) {
                throw "RequestBody with ReferenceId '$ReferenceId' does not exist in the OpenAPI document components."
            }
            # Add the request body to the MapRouteBuilder
            $requestBody = ($Embed)?
            ([Kestrun.OpenApi.OpenApiComponentClone]::Clone($requestBodies[$ReferenceId])):
            ([Microsoft.OpenApi.OpenApiRequestBodyReference]::new($ReferenceId))

            # Ensure the MapRouteBuilder is marked as having OpenAPI metadata
            $MapRouteBuilder.OpenApi[$verb].Enabled = $true

            # Add description if provided
            if ($Description) {
                $requestBody.Description = $Description
            }
            # Add the request body to the MapRouteBuilder
            $MapRouteBuilder.OpenApi[$verb].RequestBody = $requestBody
        }
        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
