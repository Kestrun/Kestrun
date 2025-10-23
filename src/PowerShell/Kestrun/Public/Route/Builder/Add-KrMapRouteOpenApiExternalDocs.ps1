<#
.SYNOPSIS
    Adds OpenAPI external documentation to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiExternalDocs cmdlet adds OpenAPI external documentation with a specified description and URL to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI external documentation will be added.
.PARAMETER Verbs
    An array of HTTP verbs (e.g., GET, POST) to which the OpenAPI external documentation will be applied. If not specified, the external documentation will be applied to all verbs defined in the Map Route Builder.
.PARAMETER Description
    A description of the OpenAPI external documentation.
.PARAMETER Url
    A URL for the OpenAPI external documentation.
.EXAMPLE
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder -Verbs @('GET') -Pattern '/api/items' |
    Add-KrMapRouteOpenApiExternalDocs -Description 'Find more info here' -Url 'https://example.com/docs'
.NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteOpenApiExternalDocs {
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
        [Uri]$url
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
            $MapRouteBuilder.OpenApi[$verb].ExternalDocs = [Microsoft.OpenApi.OpenApiExternalDocs]::new()
            if (-not [string]::IsNullOrEmpty($Description)) {
                $MapRouteBuilder.OpenApi[$verb].ExternalDocs.Description = $Description
            }
            $MapRouteBuilder.OpenApi[$verb].ExternalDocs.Url = $url
        }
        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
