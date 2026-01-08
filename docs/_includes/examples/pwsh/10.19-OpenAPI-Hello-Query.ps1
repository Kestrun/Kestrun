param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# --- Logging / Server ---

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

$srv = New-KrServer -Name 'OpenAPI Hello World' -PassThru

Add-KrEndpoint -Port $Port -IPAddress $IPAddress
# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Products API' `
    -Version '1.0.0' `
    -Description 'Demonstrates HTTP QUERY for filtered product search (OpenAPI 3.2).'

# Reusable query parameters (components)
[OpenApiParameterComponent(In = 'Query', Description = 'Page number')]
[ValidateRange(1, 1000)]
[int]$page = 1

[OpenApiParameterComponent(In = 'Query', Description = 'Page size')]
[ValidateRange(1, 100)]
[int]$pageSize = 25

# Reusable schema components
[OpenApiSchemaComponent(RequiredProperties = ('id', 'name', 'price'))]
class Product {
    [OpenApiPropertyAttribute(Description = 'Unique product identifier', Minimum = 1, Example = 101)]
    [long]$id

    [OpenApiPropertyAttribute(Description = 'Product name', Example = 'Laptop Pro')]
    [string]$name

    [OpenApiPropertyAttribute(Description = 'Product price', Format = 'double', Example = 1299.99, Minimum = 0)]
    [double]$price

    [OpenApiPropertyAttribute(Description = 'Inventory count', Minimum = 0)]
    [int]$stock
}

[OpenApiSchemaComponent()]
class ProductSearchRequest {
    [OpenApiPropertyAttribute(Description = 'Free-text search', Example = 'laptop')]
    [string]$q

    [OpenApiPropertyAttribute(Description = 'Category filter', Example = 'electronics')]
    [string]$category

    [OpenApiPropertyAttribute(Description = 'Minimum price', Format = 'double', Minimum = 0, Example = 500)]
    [double]$minPrice

    [OpenApiPropertyAttribute(Description = 'Maximum price', Format = 'double', Minimum = 0, Example = 2000)]
    [double]$maxPrice

    [OpenApiPropertyAttribute(Description = 'Only products in stock', Example = $true)]
    [bool]$inStock
}
# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger -OpenApiEndpoint "/openapi/v3.2/openapi.yaml"
Add-KrApiDocumentationRoute -DocumentType Redoc -OpenApiEndpoint "/openapi/v3.2/openapi.yaml"
Add-KrApiDocumentationRoute -DocumentType RapiDoc -OpenApiEndpoint "/openapi/v3.2/openapi.yaml"
Add-KrApiDocumentationRoute -DocumentType Scalar -OpenApiEndpoint "/openapi/v3.2/openapi.yaml"
Add-KrApiDocumentationRoute -DocumentType Elements -OpenApiEndpoint "/openapi/v3.2/openapi.yaml"



# =========================================================
#                 PRODUCT SEARCH (HTTP QUERY)
# =========================================================

<#
.SYNOPSIS
    Search products using HTTP QUERY.
.DESCRIPTION
    Demonstrates the OpenAPI 3.2 QUERY method with a structured request body
    for filters and standard query parameters for pagination.
.PARAMETER page
    Page number (query parameter component).
.PARAMETER pageSize
    Page size (query parameter component).
.PARAMETER filters
    Filter criteria passed in the QUERY request body.
#>
function searchProducts {
    [OpenApiPath(HttpVerb = 'query', Pattern = '/v1/products/search')]
    param(
        [OpenApiParameterRef(ReferenceId = 'page')]
        [int]$page,

        [OpenApiParameterRef(ReferenceId = 'pageSize')]
        [int]$pageSize,

        [OpenApiRequestBody(Description = 'Search filters', ContentType = 'application/json')]
        [ProductSearchRequest]$filters
    )

    $all = @(
        [Product]@{ id = 101; name = 'Laptop Pro'; price = 1299.99; stock = 4 }
        [Product]@{ id = 102; name = 'Laptop Air'; price = 999.00; stock = 0 }
        [Product]@{ id = 103; name = 'Phone Max'; price = 1099.50; stock = 15 }
        [Product]@{ id = 104; name = 'Desk Chair'; price = 199.99; stock = 50 }
        [Product]@{ id = 105; name = 'USB-C Hub'; price = 79.99; stock = 120 }
        [Product]@{ id = 106; name = 'Noise Headset'; price = 249.00; stock = 30 }
    )

    $filtered = $all

    if ($filters.q) {
        $q = $filters.q.ToLower()
        $filtered = $filtered | Where-Object { $_.name.ToLower().Contains($q) }
    }
    if ($filters.category) {
        # Demo: pretend electronics are items priced >= $100
        if ($filters.category -eq 'electronics') {
            $filtered = $filtered | Where-Object { $_.price -ge 100 }
        }
    }
    if ($filters.minPrice) { $filtered = $filtered | Where-Object { $_.price -ge $filters.minPrice } }
    if ($filters.maxPrice) { $filtered = $filtered | Where-Object { $_.price -le $filters.maxPrice } }
    if ($filters.inStock) { $filtered = $filtered | Where-Object { $_.stock -gt 0 } }

    $skip = ($page - 1) * $pageSize
    $paged = $filtered | Select-Object -Skip $skip -First $pageSize

    $result = [ordered]@{
        page = $page
        pageSize = $pageSize
        total = ($filtered | Measure-Object).Count
        items = $paged
    }

    # Negotiate response by Accept header (JSON/XML/YAML when supported)
    Write-KrResponse -InputObject $result -StatusCode 200
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute -DefaultVersion '3.2' # Default pattern '/openapi/{version}/openapi.{format}'

# =========================================================
#                      RUN SERVER
# =========================================================

Start-KrServer -Server $srv -CloseLogsOnExit
