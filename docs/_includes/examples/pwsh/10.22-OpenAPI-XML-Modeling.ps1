<#
    Sample: OpenAPI XML Modeling
    Purpose: Demonstrate OpenAPI 3.2 XML modeling with attributes, namespaces, and wrapped arrays.
    File:    10.22-OpenAPI-XML-Modeling.ps1
    Notes:
            - Uses OpenApiXml attribute for XML-specific configuration
      - Demonstrates:
          * XML attributes (vs. elements)
          * Custom element names
          * Namespace handling with prefixes
          * Wrapped array serialization
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# --- Logging / Server ---
New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'OpenAPI XML Modeling'

Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Product API with XML Modeling' `
    -Version '1.0.0' `
    -Description 'Demonstrates OpenAPI 3.2 XML modeling with attributes, namespaces, and wrapped arrays.'

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

# Product schema with XML-specific attributes
[OpenApiSchemaComponent(
    Title = 'Product',
    Description = 'A product with XML-specific serialization',
    RequiredProperties = ('Id', 'Name', 'Price')
)]
class Product {
    # Product ID - rendered as XML attribute
    [OpenApiProperty(Description = 'Unique product identifier', Example = 123)]
    [OpenApiXml(Name = 'id', Attribute = $true)]
    [int]$Id

    # Product name with custom element name
    [OpenApiProperty(Description = 'Product name', Example = 'Widget')]
    [OpenApiXml(Name = 'ProductName')]
    [string]$Name

    # Product price with custom namespace and prefix
    [OpenApiProperty(Description = 'Product price in USD', Example = 19.99)]
    [OpenApiXml(Name = 'Price', Namespace = 'http://example.com/pricing', Prefix = 'price')]
    [decimal]$Price

    # Array of items with wrapped XML
    [OpenApiProperty(Description = 'List of product items', Example = ('Item1', 'Item2', 'Item3'))]
    [OpenApiXml(Name = 'Item', Wrapped = $true)]
    [string[]]$Items
}

Set-KrOpenApiErrorSchema -Name 'ApiError' -ContentType @('application/json', 'application/xml')
# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

<#
.SYNOPSIS
    Get product by ID.
.DESCRIPTION
    Returns a product with XML-specific serialization.
.PARAMETER id
    Product identifier.
.NOTES
    GET endpoint: Return a product by ID
#>
function getProduct {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/products/{id}')]
    [OpenApiResponse(
        StatusCode = '200',
        Description = 'Product found',
        Schema = [Product],
        ContentType = ('application/json', 'application/xml', 'application/yaml')
    )]
    [OpenApiResponse(
        StatusCode = '404',
        Description = 'Product not found',
        ContentType = ('application/json', 'application/xml')
    )]
    param(
        [OpenApiParameter(In = 'path', Description = 'Product identifier', Required = $true)]
        [int]$id
    )

    if ($id -lt 1 -or $id -gt 100) {
        Write-KrStatusResponse -StatusCode 404
        return
    }

    $product = @{
        Id = $id
        Name = "Sample Product $id"
        Price = [decimal](19.99 + ($id * 0.50))
        Items = @('Item1', 'Item2', 'Item3')
    }

    Write-KrResponse $product -StatusCode 200
}

<#
.SYNOPSIS
    Create a new product.
.DESCRIPTION
    Accepts a product payload and returns the created product.
.PARAMETER body
    Product data.
.NOTES
    POST endpoint: Create a new product
#>
function createProduct {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/products')]
    [OpenApiResponse(
        StatusCode = '201',
        Description = 'Product created',
        Schema = [Product],
        ContentType = ('application/json', 'application/xml', 'application/yaml')
    )]
    param(
        [OpenApiRequestBody(
            Description = 'Product data',
            Required = $true,
            ContentType = ('application/json', 'application/xml', 'application/yaml')
        )]
        [Product]$body
    )

    Expand-KrObject -InputObject $body -Label 'Received Product'

    # Assign a new ID
    $body.Id = Get-Random -Minimum 1 -Maximum 1000

    Write-KrResponse $body -StatusCode 201
}

# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute

Build-KrOpenApiDocument
# Test and log OpenAPI document validation result
if (Test-KrOpenApiDocument) {
    Write-KrLog -Level Information -Message 'OpenAPI document built and validated successfully.'
} else {
    Write-KrLog -Level Error -Message 'OpenAPI document validation failed.'
}

# =========================================================
#                      RUN SERVER
# =========================================================

Start-KrServer -CloseLogsOnExit
