<#
    Sample: OpenAPI RequestBody Components
    Purpose: Demonstrate reusable request body components with multiple content types.
    File:    15.3-OpenAPI-Component-RequestBody.ps1
    Notes:   Shows class inheritance, component wrapping, and content type negotiation.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)


# --- Logging / Server ---

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

$srv = New-KrServer -Name 'OpenAPI RequestBody Component' -PassThru

Add-KrEndpoint -Port $Port -IPAddress $IPAddress
# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'RequestBody Component API' `
    -Version '1.0.0' `
    -Description 'Demonstrates reusable request body components.'

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

[OpenApiSchemaComponent(Required = ('productName', 'price'))]
class Product {
    [OpenApiPropertyAttribute(Description = 'Unique product identifier', Format = 'int64', Example = 1)]
    [long]$id

    [OpenApiPropertyAttribute(Description = 'Product name', Example = 'Laptop')]
    [string]$productName

    [OpenApiPropertyAttribute(Description = 'Product price', Format = 'double', Example = 999.99)]
    [double]$price

    [OpenApiPropertyAttribute(Description = 'Product description', Example = 'A high-performance laptop')]
    [string]$description

    [OpenApiPropertyAttribute(Description = 'Stock quantity', Minimum = 0, Example = 50)]
    [int]$stock

    [OpenApiPropertyAttribute(Description = 'Creation timestamp', Format = 'date-time', Example = '2024-01-01T12:00:00Z')]
    [string]$createdAt

    [OpenApiPropertyAttribute(Description = 'Update timestamp', Example = '2024-01-01T12:00:00Z')]
    [datetime]$updatedAt
}

# =========================================================
#        COMPONENT REQUEST BODIES (Reusable)
# =========================================================

# CreateProductRequest: RequestBody component that wraps Product schema
[OpenApiRequestBodyComponent(
    Description = 'Product creation payload. Supports JSON and form data.',
    IsRequired = $true,
    ContentType = ('application/json', 'application/x-www-form-urlencoded')
)]
class CreateProductRequest:Product {

}

# UpdateProductRequest: RequestBody component that wraps UpdateProduct schema
[OpenApiRequestBodyComponent(
    Description = 'Product update payload.',
    IsRequired = $true,
    ContentType = 'application/json',
    Required = ('productName', 'price')
)]
class UpdateProductRequest {
    [OpenApiPropertyAttribute(Description = 'Product name', Example = 'Laptop Pro')]
    [string]$productName

    [OpenApiPropertyAttribute(Description = 'Product price', Format = 'double', Example = 1299.99)]
    [double]$price

    [OpenApiPropertyAttribute(Description = 'Product description')]
    [string]$description

    [OpenApiPropertyAttribute(Description = 'Stock quantity', Minimum = 0)]
    [int]$stock
}

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

# POST endpoint: Create product using CreateProductRequest component
<#
.SYNOPSIS
    Create a new product.
.DESCRIPTION
    Creates a new product using the reusable CreateProductRequest component.
.PARAMETER body
    Product creation request
#>
function createProduct {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/products')]
    [OpenApiResponse(StatusCode = '201', Description = 'Product created successfully', Schema = [Product], ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
    [OpenApiResponse(StatusCode = '400', Description = 'Invalid input')]
    param(
        [OpenApiRequestBodyRef(ReferenceId = 'CreateProductRequest')]
        [CreateProductRequest]$body
    )

    if (-not $body.productName -or -not $body.price) {
        Write-KrResponse @{error = 'productName and price are required' } -StatusCode 400
        return
    }

    $response = [Product]@{
        id = 1
        productName = $body.productName
        price = $body.price
        description = $body.description
        stock = $body.stock -as [int]
        createdAt = (Get-Date).ToUniversalTime().ToString('o')
    }

    Write-KrResponse $response -StatusCode 201
}

# PUT endpoint: Update product using UpdateProductRequest component
<#
.SYNOPSIS
    Update an existing product.
.DESCRIPTION
    Updates product details using the reusable UpdateProductRequest component.
.PARAMETER productId
    The product ID to update
.PARAMETER body
    Product update request
#>
function updateProduct {
    [OpenApiPath(HttpVerb = 'put', Pattern = '/products/{productId}')]
    [OpenApiResponse(StatusCode = '200', Description = 'Product updated successfully', Schema = [Product], ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
    [OpenApiResponse(StatusCode = '400', Description = 'Invalid input')]
    [OpenApiResponse(StatusCode = '404', Description = 'Product not found')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true)]
        [int]$productId,
        [OpenApiRequestBodyRef(ReferenceId = 'UpdateProductRequest')]
        $body
    )

    if (-not $body.productName -or -not $body.price) {
        Write-KrJsonResponse @{error = 'productName and price are required' } -StatusCode 400
        return
    }

    $response = [Product]@{
        id = $productId
        productName = $body.productName
        price = $body.price
        description = $body.description
        stock = $body.stock -as [int]
        updatedAt = (Get-Date).ToUniversalTime().ToString('o')
    }

    Write-KrResponse $response -StatusCode 200
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


Start-KrServer -Server $srv -CloseLogsOnExit
