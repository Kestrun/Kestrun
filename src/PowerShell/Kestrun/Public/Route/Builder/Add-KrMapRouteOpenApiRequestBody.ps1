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
        [string]$ReferenceId,
        [Parameter()]
        [switch]$Force
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
            $MapRouteBuilder.OpenApi[$verb].Enabled = $true
            if ([string]::IsNullOrEmpty($ReferenceId)) {
                $MapRouteBuilder.OpenApi[$verb].RequestBody = [Microsoft.OpenApi.OpenApiRequestBody]::new()
            } else {
                $MapRouteBuilder.OpenApi[$verb].RequestBody = [Microsoft.OpenApi.OpenApiRequestBodyReference]::new($ReferenceId)
            }
            if ($Description) {
                $MapRouteBuilder.OpenApi[$verb].RequestBody.Description = $Description
            }
        }
        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
