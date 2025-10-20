<#
.SYNOPSIS
    Creates a new OpenAPI server.
.DESCRIPTION
    This function creates a new OpenAPI server using the provided parameters.
.PARAMETER Description
    A description of the server.
.PARAMETER Url
    The URL of the server.
.PARAMETER Variables
    A dictionary of server variables.
.EXAMPLE
    $variables = @{
        env = New-KrOpenApiServerVariable -Default 'dev' -Enum @('dev', 'staging', 'prod') -Description 'Environment name'
    }
    $server = New-KrOpenApiServer -Description 'My API Server' -Url 'https://api.example.com' -Variables $variables
.OUTPUTS
    Microsoft.OpenApi.OpenApiServer
#>
function New-KrOpenApiServer {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Definition')]
    param(
        [Parameter(Mandatory)]
        [string]$Url,
        [string]$Description,
        [System.Collections.Generic.Dictionary[string, Microsoft.OpenApi.OpenApiServerVariable]]$Variables
    )
    $server = [Microsoft.OpenApi.OpenApiServer]::new()
    if ($PsBoundParameters.ContainsKey('Description')) {
        $server.Description = $Description
    }
    $server.Url = $Url
    if ($PsBoundParameters.ContainsKey('Variables')) {
        $server.Variables = $Variables
    }
    return $server
}
