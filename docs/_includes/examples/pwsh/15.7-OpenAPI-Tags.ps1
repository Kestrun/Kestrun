<#
    Sample: OpenAPI Tags and External Documentation
    Purpose: Demonstrate organizing operations with tags and linking external documentation to tags.
    File:    15.7-OpenAPI-Tags.ps1
    Notes:   Shows tag-based operation organization, multi-tag assignment, and external documentation association.
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
$srv = New-KrServer -Name 'OpenAPI Tags Demo'

# Configure endpoint
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# Set document-level metadata
Add-KrOpenApiInfo -Title 'Tags Demo API' -Version '1.0.0' -Description 'Demonstrates operation tags with external documentation links.'

# Define tags with descriptions and optional external documentation links
Add-KrOpenApiTag -Name 'orders' -Description 'Operations for listing orders' -ExternalDocs (New-KrOpenApiExternalDoc -Description 'Order docs' -Url 'https://example.com/orders')
Add-KrOpenApiTag -Name 'health' -Description 'Service health endpoints'
Add-KrOpenApiTag -Name 'operations' -Description 'Common operational endpoints'

# Add top-level external documentation link
Add-KrOpenApiExternalDoc -Description 'API handbook' -Url 'https://example.com/api-handbook'

# Apply configuration and register documentation routes
Enable-KrConfiguration
Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc
Add-KrOpenApiRoute

# Route function demonstrating multiple tag assignment and tag-based organization
function getOrders {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/orders', Summary = 'List demo orders', Description = 'Returns a simple list of order identifiers.', Tags = ('orders', 'operations'))]
    [OpenApiResponse(StatusCode = '200', Description = 'List of orders', ContentType = 'application/json')]
    param()
    Write-KrJsonResponse -InputObject @{ orders = @('order-1001', 'order-2002') }
}

# Route function demonstrating multiple tag assignment
function getHealth {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/health', Summary = 'Service health', Description = 'Returns a simple health status payload.', Tags = ('health', 'operations'))]
    [OpenApiResponse(StatusCode = '200', Description = 'Health status', ContentType = 'application/json')]
    param()
    Write-KrJsonResponse -InputObject @{ status = 'healthy' }
}

# Build and validate the OpenAPI specification
Build-KrOpenApiDocument
Test-KrOpenApiDocument

# Start the server and keep logs open until exit
Start-KrServer -Server $srv -CloseLogsOnExit
Start-KrServer -Server $srv -CloseLogsOnExit
