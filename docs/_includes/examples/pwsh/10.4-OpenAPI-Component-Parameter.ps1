<#
    Sample: OpenAPI Parameter Components
    Purpose: Demonstrate reusable OpenAPI parameter components across multiple endpoints.
    File:    10.4-OpenAPI-Component-Parameter.ps1
    Notes:   Shows Query/Path/Header/Cookie parameters, parameter examples, and parameters with ContentType.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# --- Logging / Server ---

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'OpenAPI Parameter Component'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress
# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Parameter Component API' `
    -Version '1.0.0' `
    -Description 'Mini product catalog API demonstrating reusable OpenAPI parameter components.'

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

[OpenApiSchemaComponent(RequiredProperties = ('name', 'category', 'price'))]
class CreateProductRequest {
    [OpenApiPropertyAttribute(Description = 'Product name', Example = 'USB-C Dock')]
    [ValidateNotNullOrEmpty()]
    [string]$name

    [OpenApiPropertyAttribute(Description = 'Product category', Example = 'electronics')]
    [ValidateSet('electronics', 'office', 'accessories', 'other')]
    [string]$category

    [OpenApiPropertyAttribute(Description = 'Unit price', Format = 'double', Example = 159.99, Minimum = 0.01)]
    [double]$price

    [OpenApiPropertyAttribute(Description = 'Optional tags', Example = ('usb-c', 'dock'))]
    [string[]]$tags
}

[OpenApiSchemaComponent(RequiredProperties = ('id', 'name', 'category', 'price'))]
class ProductItem {
    [OpenApiPropertyAttribute(Description = 'Product ID', Format = 'int64', Example = 1)]
    [long]$id

    [OpenApiPropertyAttribute(Description = 'Product name', Example = 'Laptop')]
    [string]$name

    [OpenApiPropertyAttribute(Description = 'Product category', Example = 'electronics')]
    [string]$category

    [OpenApiPropertyAttribute(Description = 'Product price', Format = 'double', Example = 999.99)]
    [double]$price

    [OpenApiPropertyAttribute(Description = 'Product tags', Example = ('work', 'portable'))]
    [string[]]$tags
}

[OpenApiSchemaComponent()]
class ProductListResponse {
    [OpenApiPropertyAttribute(Description = 'Current page number', Example = 1, Minimum = 1)]
    [int]$page

    [OpenApiPropertyAttribute(Description = 'Items per page', Example = 20, Minimum = 1, Maximum = 100)]
    [int]$limit

    [OpenApiPropertyAttribute(Description = 'Total number of items matching the filters', Example = 5)]
    [int]$total

    [OpenApiPropertyAttribute(Description = 'List of products')]
    [ProductItem[]]$items
}

[OpenApiSchemaComponent(RequiredProperties = ('message'))]
class ErrorResponse {
    [OpenApiPropertyAttribute(Description = 'Human-readable error message', Example = 'Product not found')]
    [string]$message
}

[OpenApiSchemaComponent()]
class ClientContext {
    [OpenApiPropertyAttribute(Description = 'Client application name', Example = 'docs-sample')]
    [string]$app

    [OpenApiPropertyAttribute(Description = 'Client app version', Example = '1.2.3')]
    [string]$version

    [OpenApiPropertyAttribute(Description = 'Locale / language hint', Example = 'en-US')]
    [string]$locale
}

[OpenApiSchemaComponent(RequiredProperties = ('name'))]
class CategoryItem {
    [OpenApiPropertyAttribute(Description = 'Category name', Example = 'electronics')]
    [string]$name

    [OpenApiPropertyAttribute(Description = 'Number of products in this category', Example = 3, Minimum = 0)]
    [int]$count
}

[OpenApiSchemaComponent()]
class CategoryListResponse {
    [OpenApiPropertyAttribute(Description = 'List of categories')]
    [CategoryItem[]]$items
}

# =========================================================
#                 COMPONENT PARAMETERS
# =========================================================

# Reusable examples
New-KrOpenApiExample -Summary 'Correlation id example' -Value '2f2d68c2-9b7a-4b5c-8b1d-0fdff2a4b9a3' |
    Add-KrOpenApiInline -Name 'CorrelationIdExample'

New-KrOpenApiExample -Summary 'Client context example' -Value ([ordered]@{ app = 'docs-sample'; version = '1.2.3'; locale = 'en-US' }) |
    Add-KrOpenApiComponent -Name 'ClientContextExample'

New-KrOpenApiExample -Summary 'Create product example' -Value ([ordered]@{ name = 'USB-C Dock'; category = 'electronics'; price = 159.99; tags = @('usb-c', 'dock') }) |
    Add-KrOpenApiComponent -Name 'CreateProductRequestExample'

New-KrOpenApiExample -Summary 'Sort by price example' -Value 'price' |
    Add-KrOpenApiInline -Name 'SortByPriceExample'

# Query params (create)
[OpenApiParameterComponent(In = 'Header', Description = 'Filter by category item')]
[CategoryItem]$myCategory = NoDefault

# Header params
[OpenApiParameterComponent(In = 'Header', Description = 'Correlation id for tracing a request through logs.')]
[OpenApiParameterExampleRef(Key = 'default', ReferenceId = 'CorrelationIdExample')]
[string]$correlationId = NoDefault

# Header parameter using ContentType + schema (OpenAPI parameters can define content)
[OpenApiParameterComponent(In = 'Header', ContentType = 'application/json', Description = 'Optional client context as a JSON header.')]
[OpenApiParameterExampleRef(Key = 'example', ReferenceId = 'ClientContextExample')]
[ClientContext]$clientContext = NoDefault

# Cookie params
[OpenApiParameterComponent(In = 'Cookie', Description = 'Tenant identifier (multi-tenant demo).', Example = 'demo')]
[string]$tenantId = NoDefault

# Path params
[OpenApiParameterComponent(In = 'Path', Required = $true, Description = 'Product id.', Minimum = 1, Example = 1)]
[long]$productId = NoDefault

# Query params (list)
[OpenApiParameterComponent(In = 'Query', Description = 'Page number', Example = 1)]
[ValidateRange(0, 1000)]
[int]$page = 1

[OpenApiParameterComponent(In = 'Query', Description = 'Items per page', Minimum = 1, Maximum = 100, Example = 20)]
[int]$limit = 20

[OpenApiParameterComponent(In = 'Query', Description = 'Sort field (name, price)', Example = 'price')]
[OpenApiParameterExampleRef(Key = 'price', ReferenceId = 'SortByPriceExample')]
[ValidateSet('name', 'price')]
[string]$sortBy = 'name'

[OpenApiParameterComponent(In = 'Query', Description = 'Filter by category', Example = 'electronics')]
[ValidateSet('electronics', 'office', 'accessories', 'other', $null)]
[string]$category = NoDefault

[OpenApiParameterComponent(In = 'Query', Description = 'Filter by minimum price', Example = 100, Minimum = 0)]
[double]$minPrice = NoDefault

[OpenApiParameterComponent(In = 'Query', Description = 'Filter by maximum price', Example = 5000, Minimum = 0)]
[double]$maxPrice = NoDefault

# Query params (create)
[OpenApiParameterComponent(In = 'Query', Description = 'Validate request only (does not persist).', Example = $false)]
[bool]$dryRun = $false

# Query params (categories)
[OpenApiParameterComponent(In = 'Query', Description = 'Include per-category product counts.', Example = $true)]
[bool]$includeCounts = $true


# =========================================================
#                 SHARED IN-MEMORY STORE
# =========================================================

$Products = [System.Collections.Concurrent.ConcurrentDictionary[long, ProductItem]]::new()
$ProductIds = [System.Collections.Concurrent.ConcurrentDictionary[string, long]]::new()
$ProductIds['NextId'] = 5

$Products[1] = [ProductItem]@{ id = 1; name = 'Laptop'; category = 'electronics'; price = 999.99; tags = @('work', 'portable') }
$Products[2] = [ProductItem]@{ id = 2; name = 'Mouse'; category = 'accessories'; price = 29.99; tags = @('usb') }
$Products[3] = [ProductItem]@{ id = 3; name = 'Keyboard'; category = 'accessories'; price = 79.99; tags = @('mechanical') }
$Products[4] = [ProductItem]@{ id = 4; name = 'Monitor'; category = 'electronics'; price = 299.99; tags = @('4k') }
$Products[5] = [ProductItem]@{ id = 5; name = 'Desk Lamp'; category = 'office'; price = 49.99; tags = @('led') }

# ========================================================
Enable-KrConfiguration

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute
Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

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
    [OpenApiPath(HttpVerb = 'get', Pattern = '/v1/products', Summary = 'List products')]
    [OpenApiResponse(StatusCode = '200', Description = 'List of products', Schema = [ProductListResponse], ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponse(StatusCode = '400', Description = 'Invalid parameters')]
    param(

        [OpenApiParameterRef(ReferenceId = 'myCategory')]
        [CategoryItem]$myCategory,

        [OpenApiParameterRef(ReferenceId = 'correlationId')]
        [string]$correlationId,

        [OpenApiParameterRef(ReferenceId = 'clientContext')]
        [ClientContext]$clientContext,

        [OpenApiParameterRef(ReferenceId = 'tenantId')]
        [string]$tenantId,

        [OpenApiParameterRef(ReferenceId = 'page')]
        [int]$page,

        [OpenApiParameterRef(ReferenceId = 'limit')]
        [int]$limit,

        [OpenApiParameterRef(ReferenceId = 'sortBy')]
        [string]$sortBy,

        [OpenApiParameterRef(ReferenceId = 'category')]
        [string]$category,

        [OpenApiParameterRef(ReferenceId = 'minPrice')]
        [double]$minPrice,

        [OpenApiParameterRef(ReferenceId = 'maxPrice')]
        [double]$maxPrice
    )
    if (-not [string]::IsNullOrWhiteSpace($correlationId)) {
        $Context.Response.Headers['correlationId'] = $correlationId
    }
    Expand-KrObject -InputObject $myCategory -Label 'My Category Parameter'
    write-KrLog -Level Debug -Message 'TenantId: {tenantId}' -Values $tenantId
    Expand-KrObject -InputObject $clientContext -Label 'Client Context'
    # Read shared store
    $allProducts = $Products.Values
    Expand-KrObject -InputObject $allProducts -Label 'All Products'
    # Apply filters
    $filtered = $allProducts
    if ($category) {
        Write-KrLog -Level Debug -Message 'Filtering by category: {category}' -Values $category
        $filtered = $filtered | Where-Object { $_.category -eq $category }
    }

    if ( $minPrice -gt 0) {
        Write-KrLog -Level Debug -Message 'Filtering by minPrice: {minPrice}' -Values $minPrice
        $filtered = $filtered | Where-Object { $_.price -ge [double]$minPrice }
    }
    if ($maxPrice -gt 0) {
        Write-KrLog -Level Debug -Message 'Filtering by maxPrice: {maxPrice}' -Values $maxPrice
        $filtered = $filtered | Where-Object { $_.price -le [double]$maxPrice }
    }

    # Sort
    if ($sortBy -eq 'price') {
        $filtered = $filtered | Sort-Object -Property price
    } else {
        $filtered = $filtered | Sort-Object -Property name
    }

    # Paginate
    $page = [int]$page
    $limit = [int]$limit
    $skip = ($page - 1) * $limit
    Expand-KrObject -InputObject $filtered -Label 'Filtered Products'
    $paged = $filtered | Select-Object -Skip $skip -First $limit
    Expand-KrObject -InputObject $paged -Label "Products Page $page (Limit $limit)"
    # Convert to ProductItem array (ensure typed for OpenAPI)
    $productItems = $paged | ForEach-Object {
        [ProductItem]@{
            id = $_.id
            name = $_.name
            category = $_.category
            price = $_.price
            tags = $_.tags
        }
    }
    Expand-KrObject -InputObject $productItems -Label 'Product Items'
    # Build typed response
    $response = [ProductListResponse]@{
        page = $page
        limit = $limit
        total = $filtered.Count
        items = $productItems
    }
    Expand-KrObject -InputObject $response -Label 'Response Object'
    Write-KrResponse $response -StatusCode 200
}

<#
.SYNOPSIS
    Get a product by id.
.DESCRIPTION
    Retrieves a single product from the in-memory catalog.
.PARAMETER productId
    Product id (path parameter component).
#>

function getProduct {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/v1/products/{productId}', Summary = 'Get product')]
    [OpenApiResponse(StatusCode = '200', Description = 'The product', Schema = [ProductItem], ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponse(StatusCode = '404', Description = 'Not found', Schema = [ErrorResponse])]
    param(
        [OpenApiParameterRef(ReferenceId = 'correlationId')]
        [string]$correlationId,

        [OpenApiParameterRef(ReferenceId = 'tenantId')]
        [string]$tenantId,

        [OpenApiParameterRef(ReferenceId = 'productId')]
        [long]$productId
    )

    if (-not [string]::IsNullOrWhiteSpace($correlationId)) {
        $Context.Response.Headers['correlationId'] = $correlationId
    }
    Write-KrLog -Level Debug -Message 'TenantId: {tenantId}' -Values $tenantId
    $item = NoDefault
    if (-not $Products.TryGetValue($productId, [ref]$item)) {
        Write-KrResponse ([ErrorResponse]@{ message = 'Product not found' }) -StatusCode 404
        return
    }

    Write-KrResponse $item -StatusCode 200
}

<#
.SYNOPSIS
    Create a product.
.DESCRIPTION
    Validates and creates a product in the in-memory catalog.
.PARAMETER body
    The product creation payload.
.PARAMETER dryRun
    If true, validates the request but does not persist.
#>

function createProduct {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/v1/products', Summary = 'Create product')]
    [OpenApiResponse(StatusCode = '201', Description = 'Created product', Schema = [ProductItem], ContentType = ('application/json', 'application/xml'))]
    [OpenApiResponse(StatusCode = '400', Description = 'Invalid request', Schema = [ErrorResponse])]
    param(
        [OpenApiParameterRef(ReferenceId = 'correlationId')]
        [string]$correlationId,

        [OpenApiParameterRef(ReferenceId = 'tenantId')]
        [string]$tenantId,

        [OpenApiParameterRef(ReferenceId = 'dryRun')]
        [bool]$dryRun,

        [OpenApiRequestBody(ContentType = 'application/json', Required = $true, Description = 'Product create request')]
        [OpenApiRequestBodyExampleRef(Key = 'default', ReferenceId = 'CreateProductRequestExample')]
        [CreateProductRequest]$body
    )

    Write-KrLog -Level Debug -Message 'Creating product for tenant: {tenantId}' -Values $tenantId
    if (-not [string]::IsNullOrWhiteSpace($correlationId)) {
        $Context.Response.Headers['correlationId'] = $correlationId
    }

    if ([string]::IsNullOrWhiteSpace($body.name)) {
        Write-KrResponse ([ErrorResponse]@{ message = 'name is required' }) -StatusCode 400
        return
    }

    if ($body.price -le 0) {
        Write-KrResponse ([ErrorResponse]@{ message = 'price must be > 0' }) -StatusCode 400
        return
    }

    if ($dryRun) {
        $preview = [ProductItem]@{
            id = 0
            name = $body.name
            category = $body.category
            price = $body.price
            tags = $body.tags
        }
        Write-KrResponse $preview -StatusCode 201
        return
    }

    $newId = $ProductIds.AddOrUpdate('NextId', 1, { param($k, $v) $v + 1 })
    $created = [ProductItem]@{
        id = [long]$newId
        name = $body.name
        category = $body.category
        price = $body.price
        tags = $body.tags
    }

    $Products[[long]$newId] = $created
    $Context.Response.Headers['Location'] = "/v1/products/$newId"

    Write-KrResponse $created -StatusCode 201
}

<#
.SYNOPSIS
    List categories.
.DESCRIPTION
    Lists distinct categories from the current catalog.
.PARAMETER includeCounts
    When true, include counts per category.
#>
function listCategories {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/v1/categories', Summary = 'List categories')]
    [OpenApiResponse(StatusCode = '200', Description = 'Category list', Schema = [CategoryListResponse], ContentType = ('application/json', 'application/xml'))]
    param(
        [OpenApiParameterRef(ReferenceId = 'correlationId')]
        [string]$correlationId,

        [OpenApiParameterRef(ReferenceId = 'includeCounts')]
        [bool]$includeCounts
    )

    if (-not [string]::IsNullOrWhiteSpace($correlationId)) {
        $Context.Response.Headers['correlationId'] = $correlationId
    }

    $values = $Products.Values
    $groups = $values | Group-Object -Property category | Sort-Object -Property Name

    $items = foreach ($g in $groups) {
        [CategoryItem]@{
            name = $g.Name
            count = if ($includeCounts) { $g.Count } else { 0 }
        }
    }

    Write-KrResponse ([CategoryListResponse]@{ items = $items }) -StatusCode 200
}

# =========================================================
#                      RUN SERVER
# =========================================================

Start-KrServer -CloseLogsOnExit
