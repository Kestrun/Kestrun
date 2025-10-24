<#
    .SYNOPSIS
        Adds a new Swagger UI route to the Kestrun server.
    .DESCRIPTION
        This function allows you to add a new Swagger UI route to the Kestrun server by specifying the route path and the OpenAPI endpoint.
    .PARAMETER Server
        The Kestrun server instance to which the route will be added.
        If not specified, the function will attempt to resolve the current server context.
    .PARAMETER Options
        The MapRouteOptions object to configure the route.
    .PARAMETER Pattern
        The URL path for the new Swagger UI route. Default is '/docs/swagger'.
    .PARAMETER OpenApiEndpoint
        The OpenAPI endpoint URI that the Swagger UI will use to fetch the API documentation. Default is '/openapi/v3.0/openapi.json'.
    .PARAMETER PassThru
        If specified, the function will return the created route object.
    .OUTPUTS
        [Microsoft.AspNetCore.Builder.IEndpointConventionBuilder] representing the created route.
    .EXAMPLE
        Add-KrSwaggerUiRoute -Server $myServer -Pattern "/docs/swagger" -OpenApiEndpoint "/openapi/v3.0/openapi.json"
        Adds a new Swagger UI route to the specified Kestrun server with the given pattern and OpenAPI endpoint.
    .EXAMPLE
        Get-KrServer | Add-KrSwaggerUiRoute -Pattern "/docs/swagger" -OpenApiEndpoint "/openapi/v3.0/openapi.json" -PassThru
    .NOTES
        This function is part of the Kestrun PowerShell module and is used to manage routes
#>
function Add-KrSwaggerUiRoute {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [Kestrun.Hosting.Options.MapRouteOptions]$Options,
        [Parameter()]
        [alias('Path')]
        [string]$Pattern,
        [Parameter()]
        [Uri]$OpenApiEndpoint = [Uri]::new('/openapi/v3.0/openapi.json', [UriKind]::Relative),
        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($null -eq $Options) {
            $Options = [Kestrun.Hosting.Options.MapRouteOptions]::new()
        }
        if ([string]::IsNullOrEmpty($Pattern)) {
            $Options.Pattern = '/docs/swagger'
        } else {

            $Options.Pattern = $Pattern
        }

        # Call the C# extension method to add the Swagger UI route
        [Kestrun.Hosting.KestrunHostMapExtensions]::AddSwaggerUiRoute($Server, $Options, $OpenApiEndpoint) | Out-Null
        if ($PassThru) {
            return $Server
        }
    }
}
