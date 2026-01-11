<#
    Sample: OpenAPI Tags and External Documentation
    Purpose: Demonstrate organizing operations with hierarchical (OpenAPI 3.2) tags and linking external documentation to tags.
    File:    10.7-OpenAPI-Tags.ps1
    Notes:   Shows hierarchical tags (parent/kind), tag extensions, and externalDocs extensions.
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# Configure logging
New-KrLogger |
    Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Information |
    Register-KrLogger -Name 'console' -SetAsDefault | Out-Null

# Create server instance
New-KrServer -Name 'OpenAPI Tags Demo'

# Configure endpoint
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Set document-level metadata
Add-KrOpenApiInfo -Title 'Tags Demo API' -Version '1.0.0' -Description 'Demonstrates operation tags with external documentation links.'

# Define hierarchical tags (OpenAPI 3.2) and attach extensions.
# Note: Extension keys are normalized to the required `x-` prefix.

$opsTagExtensions = [ordered]@{
    'x-displayName' = 'Operations'
    'x-sortOrder' = 1
    'icon' = 'tools'
}

$ordersTagExtensions = [ordered]@{
    'x-displayName' = 'Orders'
    'x-sortOrder' = 10
    'x-owner' = 'commerce-team'
    'releaseStage' = 'beta'
}

$healthTagExtensions = [ordered]@{
    'x-displayName' = 'Health'
    'x-sortOrder' = 20
    'x-owner' = 'platform-team'
}

$ordersExternalDocsExtensions = [ordered]@{
    'x-docType' = 'reference'
    'audience' = 'public'
}

$ordersExternalDocs = New-KrOpenApiExternalDoc -Description 'Order docs' -Url 'https://example.com/orders' -Extensions $ordersExternalDocsExtensions

$apiPortalExtensions = [ordered]@{
    'x-docType' = 'portal'
    'audience' = 'internal'
    'x-owner' = 'api-platform'
}

# Root tag
Add-KrOpenApiTag -Name 'operations' -Description 'Common operational endpoints' -Kind 'category' -Extensions $opsTagExtensions

# Resource tags
Add-KrOpenApiTag -Name 'orders' -Description 'Order operations' -Parent 'operations' -Kind 'resource' -ExternalDocs $ordersExternalDocs -Extensions $ordersTagExtensions
Add-KrOpenApiTag -Name 'health' -Description 'Service health endpoints' -Parent 'operations' -Kind 'resource' -Extensions $healthTagExtensions

# Leaf tags (operation groups) under resources
Add-KrOpenApiTag -Name 'orders.read' -Description 'Read-only order operations' -Parent 'orders' -Kind 'operation' -Extensions ([ordered]@{ 'x-scope' = 'orders:read' })
Add-KrOpenApiTag -Name 'orders.write' -Description 'Mutating order operations' -Parent 'orders' -Kind 'operation' -Extensions ([ordered]@{ 'x-scope' = 'orders:write' })
Add-KrOpenApiTag -Name 'health.status' -Description 'Health status endpoints' -Parent 'health' -Kind 'operation' -Extensions ([ordered]@{ 'x-scope' = 'health:read' })

# Add top-level external documentation link (document-level externalDocs + extensions)
Add-KrOpenApiExternalDoc -Description 'API portal' -Url 'https://example.com/api-portal' -Extensions $apiPortalExtensions

# Apply configuration and register documentation routes
Enable-KrConfiguration
Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint '/openapi/v3.1/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Elements -OpenApiEndpoint '/openapi/v3.2/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Scalar -OpenApiEndpoint '/openapi/v3.2/openapi.json'
Add-KrApiDocumentationRoute -DocumentType Rapidoc -OpenApiEndpoint '/openapi/v3.2/openapi.json'

Add-KrOpenApiRoute

<#
.SYNOPSIS
    Retrieves a list of demo orders.
.DESCRIPTION
    This endpoint returns a simple list of order identifiers.
    It is tagged with a hierarchical tag ('orders.read') plus its parents ('orders', 'operations').
#>
function getOrders {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/orders', Summary = 'List demo orders', Description = 'Returns a simple list of order identifiers.', Tags = ('orders.read', 'orders', 'operations'))]
    [OpenApiResponse(StatusCode = '200', Description = 'List of orders', ContentType = 'application/json')]
    param()
    Write-KrJsonResponse -InputObject @{ orders = @('order-1001', 'order-2002') }
}

<#.SYNOPSIS
    Retrieves the health status of the service.
.DESCRIPTION
    This endpoint returns a simple health status payload.
    It is tagged with a hierarchical tag ('health.status') plus its parents ('health', 'operations').
#>
function getHealth {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/health', Summary = 'Service health', Description = 'Returns a simple health status payload.', Tags = ('health.status', 'health', 'operations'))]
    [OpenApiResponse(StatusCode = '200', Description = 'Health status', ContentType = 'application/json')]
    param()
    Write-KrJsonResponse -InputObject @{ status = 'healthy' }
}

# Build and validate the OpenAPI specification
Build-KrOpenApiDocument
Test-KrOpenApiDocument

# Start the server and keep logs open until exit
Start-KrServer -CloseLogsOnExit

