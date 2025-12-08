param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# --- Logging / Server ---

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

$srv = New-KrServer -Name 'OpenAPI Parameter Component' -PassThru

Add-KrEndpoint -Port $Port -IPAddress $IPAddress
# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Parameter Component API' `
    -Version '1.0.0' `
    -Description 'Demonstrates reusable parameter components.'

# =========================================================
#                 COMPONENT PARAMETERS
# =========================================================

# Define reusable parameter components using class attributes
[OpenApiParameterComponent()]
class PaginationParameters {
    [OpenApiParameter(In = [OaParameterLocation]::Query, Description = 'Page number')]

    [OpenApiPropertyAttribute(Minimum = 1, Example = 1)]
    [int]$page

    [OpenApiParameter(In = [OaParameterLocation]::Query, Description = 'Items per page')]
    [OpenApiPropertyAttribute(Minimum = 1, Maximum = 100, Example = 20)]
    [int]$limit

    [OpenApiParameter(In = [OaParameterLocation]::Query, Description = 'Sort field (name, date, price)')]
    [OpenApiPropertyAttribute(Example = 'date')]
    [ValidateSet('name', 'date', 'price')]
    [string]$sortBy

    [OpenApiParameter(In = [OaParameterLocation]::Query, Description = 'Filter by category')]
    [OpenApiPropertyAttribute(Example = 'electronics')]
    [string]$category

    [OpenApiParameter(In = [OaParameterLocation]::Query, Description = 'Filter by minimum price')]
    [OpenApiPropertyAttribute(Format = 'double', Example = 100)]
    [double]$minPrice

    [OpenApiParameter(In = [OaParameterLocation]::Query, Description = 'Filter by maximum price')]
    [OpenApiPropertyAttribute(Format = 'double', Example = 5000)]
    [double]$maxPrice
}

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

[OpenApiSchemaComponent()]
class ProductItem {
    [OpenApiPropertyAttribute(Description = 'Product ID', Format = 'int64', Example = 1)]
    [long]$id

    [OpenApiPropertyAttribute(Description = 'Product name', Example = 'Laptop')]
    [string]$name

    [OpenApiPropertyAttribute(Description = 'Product category', Example = 'electronics')]
    [string]$category

    [OpenApiPropertyAttribute(Description = 'Product price', Format = 'double', Example = 999.99)]
    [double]$price
}

[OpenApiSchemaComponent()]
class ProductListResponse {
    [OpenApiPropertyAttribute(Description = 'Current page number', Example = 1)]
    [int]$page

    [OpenApiPropertyAttribute(Description = 'Items per page', Example = 20)]
    [int]$limit

    [OpenApiPropertyAttribute(Description = 'Total number of items', Example = 5)]
    [int]$total

    [OpenApiPropertyAttribute(Description = 'List of products')]
    [ProductItem[]]$items
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
    [OpenApiResponse(StatusCode = '200', Description = 'List of products', Schema = [ProductListResponse], ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponse(StatusCode = '400', Description = 'Invalid parameters')]
    param(
        [OpenApiParameterRef(ReferenceId = 'page')]
        [int]$page = 1,

        [OpenApiParameterRef(ReferenceId = 'limit')]
        [int]$limit = 20,

        [OpenApiParameterRef(ReferenceId = 'sortBy')]
        [string]$sortBy = 'date',

        [OpenApiParameterRef(ReferenceId = 'category')]
        [string]$category,

        [OpenApiParameterRef(ReferenceId = 'minPrice')]
        [double]$minPrice,

        [OpenApiParameterRef(ReferenceId = 'maxPrice')]
        [double]$maxPrice
    )

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

    # Convert to ProductItem array
    $productItems = $paged | ForEach-Object {
        [ProductItem]@{
            id = $_.id
            name = $_.name
            category = $_.category
            price = $_.price
        }
    }

    # Build typed response
    $response = [ProductListResponse]@{
        page = $page
        limit = $limit
        total = $filtered.Count
        items = $productItems
    }

    Write-KrResponse $response -StatusCode 200
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute

# =========================================================
#                      RUN SERVER
# =========================================================

Start-KrServer -Server $srv -CloseLogsOnExit
