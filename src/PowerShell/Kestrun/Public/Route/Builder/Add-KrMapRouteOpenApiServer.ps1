<#
.SYNOPSIS
    Adds OpenAPI server information to a Map Route Builder.
.DESCRIPTION
    The Add-KrMapRouteOpenApiServer cmdlet adds OpenAPI server information to a Map Route Builder object.
.PARAMETER MapRouteBuilder
    The Map Route Builder object to which the OpenAPI server information will be added.
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
        [Parameter(Mandatory = $true)]
        [Microsoft.OpenApi.OpenApiServer[]]$Server
    )
    process {
        $MapRouteBuilder.OpenApi.Enabled = $true
        foreach ($s in $Server) {
            $MapRouteBuilder.OpenApi.Servers.Add($s)
        }

        # Return the modified MapRouteBuilder for pipeline chaining
        return $MapRouteBuilder
    }
}
