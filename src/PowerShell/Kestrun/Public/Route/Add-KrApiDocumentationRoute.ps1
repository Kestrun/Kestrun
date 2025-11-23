<#
    .SYNOPSIS
        Adds a new API documentation route (Redoc or Swagger UI) to the Kestrun server.
    .DESCRIPTION
        This function allows you to add a new API documentation route to the Kestrun server by specifying the route path, document type, and the OpenAPI endpoint.
    .PARAMETER Server
        The Kestrun server instance to which the route will be added.
        If not specified, the function will attempt to resolve the current server context.
    .PARAMETER Options
        The MapRouteOptions object to configure the route.
    .PARAMETER Pattern
        The URL path for the new API documentation route.
        Default is '/docs/redoc' for Redoc and '/docs/swagger' for Swagger UI.
    .PARAMETER DocumentType
        The type of API documentation to add. Valid values are 'Redoc' and 'Swagger'. Default is 'Swagger'.
    .PARAMETER OpenApiEndpoint
        The OpenAPI endpoint URI that the documentation UI will use to fetch the API documentation. Default is '/openapi/v3.0/openapi.json'.
    .PARAMETER PassThru
        If specified, the function will return the created route object.
    .OUTPUTS
        [Microsoft.AspNetCore.Builder.IEndpointConventionBuilder] representing the created route.
    .EXAMPLE
        Add-KrApiDocumentationRoute -Server $myServer -Pattern "/docs/redoc" -DocumentType "Redoc" -OpenApiEndpoint "/openapi/v3.0/openapi.json"
        Adds a new Redoc UI route to the specified Kestrun server with the given pattern and OpenAPI endpoint.
    .EXAMPLE
        Get-KrServer | Add-KrApiDocumentationRoute -Pattern "/docs/swagger" -DocumentType "Swagger" -OpenApiEndpoint "/openapi/v3.0/openapi.json" -PassThru
    .NOTES
        This function is part of the Kestrun PowerShell module and is used to manage routes
#>
function Add-KrApiDocumentationRoute {
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
        [ValidateSet('Redoc', 'Swagger')]
        [string]$DocumentType = 'Swagger',
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

        $Options.Pattern = $Pattern
        switch ($DocumentType) {
            'Swagger' {
                # Call the C# extension method to add the Swagger UI route
                [Kestrun.Hosting.KestrunHostMapExtensions]::AddSwaggerUiRoute($Server, $Options, $OpenApiEndpoint) | Out-Null
            }
            'Redoc' {
                # Call the C# extension method to add the Redoc UI route
                [Kestrun.Hosting.KestrunHostMapExtensions]::AddRedocUiRoute($Server, $Options, $OpenApiEndpoint) | Out-Null
            }
            default {
                throw "Unsupported DocumentType: $DocumentType. Supported types are 'Swagger' and 'Redoc'."
            }
        }
        if ($PassThru) {
            return $Server
        }
    }
}
