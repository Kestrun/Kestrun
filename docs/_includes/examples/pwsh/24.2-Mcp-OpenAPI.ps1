using namespace Kestrun
using namespace Kestrun.OpenApi

param(
    [int]$Port = $env:PORT ?? 5001
)

New-KrServer -Name 'MCP OpenAPI'

Add-KrEndpoint -Port $Port

Add-KrOpenApiInfo -Title 'Items API' `
    -Version '1.0.0' `
    -Description 'A minimal OpenAPI-aware route for MCP inspection.'

Enable-KrConfiguration

<#
.SYNOPSIS
    Create an item.
#>
function NewItemRoute {
    [OpenApiPath(Pattern = '/items/{id}', HttpVerb = 'post', OperationId = 'createItem', Tags = @('items'))]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true)]
        [string] $id,

        [OpenApiRequestBody(ContentType = @('application/json'), Required = $true)]
        [object] $Body
    )

    Write-KrResponse @{
        itemId = $id
        ok = $true
    }
}

Add-KrOpenApiRoute

Start-KrServer
