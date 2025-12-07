param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

if (-not (Get-Module Kestrun)) { Import-Module Kestrun }

# --- Logging / Server ---

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

$srv = New-KrServer -Name 'OpenAPI Parameter Component' -PassThru

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Parameter Component API' `
    -Version '1.0.0' `
    -Description 'Demonstrates reusable parameter components.'

Add-KrOpenApiContact -Email 'support@example.com'
Add-KrOpenApiServer -Url "http://$($IPAddress):$Port" -Description 'Local Server'

# =========================================================
#                 COMPONENT PARAMETERS
# =========================================================

# Define reusable parameter components using class attributes
[OpenApiParameterComponent()]
class PaginationParameters {
    [OpenApiPropertyAttribute(Description = 'Page number', Minimum = 1, Example = 1)]
    [int]$page

    [OpenApiPropertyAttribute(Description = 'Items per page', Minimum = 1, Maximum = 100, Example = 20)]
    [int]$limit

    [OpenApiPropertyAttribute(Description = 'Sort field (name, date, price)', Example = 'date')]
    [ValidateSet('name', 'date', 'price')]
    [string]$sortBy
}

[OpenApiParameterComponent()]
class FilterParameters {
    [OpenApiPropertyAttribute(Description = 'Filter by category', Example = 'electronics')]
    [string]$category

    [OpenApiPropertyAttribute(Description = 'Filter by minimum price', Format = 'double', Example = 100)]
    [double]$minPrice

    [OpenApiPropertyAttribute(Description = 'Filter by maximum price', Format = 'double', Example = 5000)]
    [double]$maxPrice
}

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

# GET endpoint with reusable parameter components
<#
.SYNOPSIS
    List products with filters and pagination.
.DESCRIPTION
    Retrieves a paginated list of products with optional filtering and sorting.
.PARAMETER page
    Page number (pagination component)
.PARAMETER limit
    Items per page (pagination component)
.PARAMETER sortBy
    Sort field (pagination component)
.PARAMETER category
    Filter by category (filter component)
.PARAMETER minPrice
    Minimum price filter (filter component)
.PARAMETER maxPrice
    Maximum price filter (filter component)
#>
function listProducts {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/products')]
    [OpenApiResponse(StatusCode = '200', Description = 'List of products', ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponse(StatusCode = '400', Description = 'Invalid parameters')]
    param([int]$page = 1, [int]$limit = 20, [string]$sortBy = 'date', [string]$category, [double]$minPrice, [double]$maxPrice)

    # Mock product data
    $allProducts = @(
        @{ id = 1; name = 'Laptop'; category = 'electronics'; price = 999.99 }
        @{ id = 2; name = 'Mouse'; category = 'electronics'; price = 29.99 }
        @{ id = 3; name = 'Keyboard'; category = 'electronics'; price = 79.99 }
        @{ id = 4; name = 'Monitor'; category = 'electronics'; price = 299.99 }
        @{ id = 5; name = 'Desk Lamp'; category = 'office'; price = 49.99 }
    )

    # Apply filters
    $filtered = $allProducts
    if ($category) {
        $filtered = $filtered | Where-Object { $_.category -eq $category }
    }
    if ($minPrice) {
        $filtered = $filtered | Where-Object { $_.price -ge [double]$minPrice }
    }
    if ($maxPrice) {
        $filtered = $filtered | Where-Object { $_.price -le [double]$maxPrice }
    }

    # Sort
    if ($sortBy -eq 'price') {
        $filtered = $filtered | Sort-Object -Property price
    } elseif ($sortBy -eq 'name') {
        $filtered = $filtered | Sort-Object -Property name
    }

    # Paginate
    $page = [int]$page
    $limit = [int]$limit
    $skip = ($page - 1) * $limit
    $paged = $filtered | Select-Object -Skip $skip -First $limit

    $response = @{
        page = $page
        limit = $limit
        total = $filtered.Count
        items = $paged
    }

    Write-KrJsonResponse $response -StatusCode 200
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute

Build-KrOpenApiDocument
Test-KrOpenApiDocument

# =========================================================
#                      RUN SERVER
# =========================================================

Add-KrEndpoint -Port $Port -IPAddress $IPAddress
Start-KrServer -Server $srv -CloseLogsOnExit
