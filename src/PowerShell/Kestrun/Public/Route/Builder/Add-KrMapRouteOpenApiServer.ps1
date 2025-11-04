<#
.SYNOPSIS
    Adds OpenAPI server information to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiServer cmdlet adds OpenAPI server information to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI server information will be added.
.PARAMETER Verbs
    An array of HTTP verbs (e.g., GET, POST) to which the OpenAPI server information will be applied. If not specified, the server information will be applied to all verbs defined in the Map Route Builder.
.PARAMETER Server
    An array of OpenAPI server objects to be associated with the route.
.EXAMPLE
    $openApiServer1 = New-KrOpenApiServer -Url 'https://api.example.com/v1' -Description 'Production Server'
    $openApiServer2 = New-KrOpenApiServer -Url 'https://staging-api.example.com/v1' -Description 'Staging Server'
    # Create a new Map Route Builder
    $mapRouteBuilder = New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items' |
    Add-KrMapRouteOpenApiServer -Server $openApiServer1, $openApiServer2
 .NOTES
    This cmdlet is part of the route builder module.
#>
function Add-KrMapRouteOpenApiServer {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.Options.MapRouteBuilder])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter()]
        [Kestrun.Utilities.HttpVerb[]]$Verbs,
        [Parameter(Mandatory = $true)]
        [Microsoft.OpenApi.OpenApiServer[]]$Server
    )
    process {
        # Determine if we are using path-level metadata
        $usePathLevel = $Verbs.Count -eq 0

        # Add servers to the MapRouteBuilder
        if ($usePathLevel) {
            # Add to path-level if no verbs specified
            if ($null -eq $MapRouteBuilder.PathLevelOpenAPIMetadata) {
                $MapRouteBuilder.PathLevelOpenAPIMetadata = [Kestrun.Hosting.Options.OpenAPICommonMetadata]::new()
            }
            # Add servers to the path-level metadata
            foreach ($s in $Server) {
                $MapRouteBuilder.PathLevelOpenAPIMetadata.Servers.Add($s)
            }
        } else {
            foreach ($verb in $Verbs) {
                if (-not $MapRouteBuilder.OpenApi.ContainsKey($verb)) {
                    $MapRouteBuilder.OpenApi[$verb] = [Kestrun.Hosting.Options.OpenAPIMetadata]::new($MapRouteBuilder.Pattern)
                }
                $MapRouteBuilder.OpenApi[$verb].Enabled = $true
                # Add servers to the specified verb
                foreach ($s in $Server) {
                    $MapRouteBuilder.OpenApi[$verb].Servers.Add($s)
                }
            }
        }
        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
