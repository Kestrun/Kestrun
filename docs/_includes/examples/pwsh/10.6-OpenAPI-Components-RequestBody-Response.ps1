<#
    Sample: OpenAPI RequestBody & Response Components
    Purpose: Demonstrate complete components integration with request bodies and response schemas.
    File:    10.6-OpenAPI-Components-RequestBody-Response.ps1
    Notes:   Shows component wrapping, CRUD operations, and complete error handling.
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)


# --- Logging / Server ---

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

$srv = New-KrServer -Name 'OpenAPI RequestBody & Response Components' -PassThru
Add-KrEndpoint -Port $Port -IPAddress $IPAddress
# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Complete Components API' `
    -Version '1.0.0' `
    -Description 'Demonstrates complete reusable components: request bodies and responses together.'


Add-KrOpenApiTag -Name 'orders' -Description 'Order management operations'

# =========================================================
#                      COMPONENT SCHEMAS
# =========================================================

# Request schema for creating an order
[OpenApiSchemaComponent(RequiredProperties = ('productId', 'quantity'))]
class CreateOrderRequest {
    [OpenApiPropertyAttribute(Description = 'Product ID', Example = 1)]
    [long]$productId

    [OpenApiPropertyAttribute(Description = 'Quantity ordered', Minimum = 1, Example = 5)]
    [int]$quantity

    [OpenApiPropertyAttribute(Description = 'Customer email', Format = 'email', Example = 'customer@example.com')]
    [string]$customerEmail

    [OpenApiPropertyAttribute(Description = 'Shipping address')]
    [string]$shippingAddress
}

# Response schema for order data
[OpenApiSchemaComponent(RequiredProperties = ('orderId', 'productId', 'quantity', 'status', 'totalPrice'))]
class OrderResponse {
    [OpenApiPropertyAttribute(Description = 'Order ID', Example = 'a54a57ca-36f8-421b-a6b4-2e8f26858a4c')]
    [Guid]$orderId

    [OpenApiPropertyAttribute(Description = 'Product ID', Format = 'int64', Example = 1)]
    [long]$productId

    [OpenApiPropertyAttribute(Description = 'Quantity ordered', Example = 5)]
    [int]$quantity

    [OpenApiPropertyAttribute(Description = 'Order status'  )]
    [ValidateSet('pending', 'processing', 'shipped', 'delivered')]
    [string]$status

    [OpenApiPropertyAttribute(Description = 'Total price', Format = 'double', Example = 499.95)]
    [double]$totalPrice

    [OpenApiPropertyAttribute(Description = 'Order creation date', Format = 'date-time')]
    [string]$createdAt

    [OpenApiPropertyAttribute(Description = 'Expected delivery date', Format = 'date')]
    [string]$expectedDelivery
}

# Error response schema
[OpenApiSchemaComponent(RequiredProperties = ('code', 'message'))]
class ErrorDetail {
    [OpenApiPropertyAttribute(Description = 'Error code', Example = 'INVALID_QUANTITY')]
    [string]$code

    [OpenApiPropertyAttribute(Description = 'Error message', Example = 'Quantity must be greater than zero')]
    [string]$message

    [OpenApiPropertyAttribute(Description = 'Field that caused the error')]
    [string]$field

    [OpenApiPropertyAttribute(Description = 'Additional context')]
    [string]$details
}

# =========================================================
#     COMPONENT REQUEST BODIES & RESPONSES (Reusable)
# =========================================================

# CreateOrderRequest: RequestBody component
[OpenApiRequestBodyComponent(
    Description = 'Order creation payload',
    Required = $true,
    ContentType = 'application/json'
)]
class CreateOrderRequestBody:CreateOrderRequest {}

# Response components (variable-based)
[OpenApiResponseComponent(Description = 'Order successfully retrieved or created', ContentType = ('application/json', 'application/xml'))]
[OrderResponse]$OrderResponseDefault = NoDefault

[OpenApiResponseComponent(Description = 'Request validation error', ContentType = ('application/json', 'application/xml'))]
[ErrorDetail]$ErrorResponseDefault = NoDefault

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc


<#
.SYNOPSIS
    Create a new order.
.DESCRIPTION
    Creates a new order using the reusable CreateOrderRequestBody component.
    Returns OrderResponse on success or ErrorDetail on failure.
.PARAMETER body
    Order creation request using CreateOrderRequestBody component
.NOTES
    Tags: orders
    POST /orders: Create order
#>
function createOrder {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/orders')]
    [OpenApiResponseRefAttribute(StatusCode = '201', ReferenceId = 'OrderResponseDefault')]
    [OpenApiResponseRefAttribute(StatusCode = '400', ReferenceId = 'ErrorResponseDefault')]
    param(
        [OpenApiRequestBody(ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
        [CreateOrderRequestBody]$body
    )

    # Validate required fields
    if (-not $body.productId -or -not $body.quantity) {
        $myError = [ErrorDetail] @{
            code = 'MISSING_FIELDS'
            message = 'productId and quantity are required'
            field = 'productId,quantity'
        }
        Write-KrResponse $myError -StatusCode 400
        return
    }

    # Validate quantity
    if ([int]$body.quantity -le 0) {
        $myError = [ErrorDetail]@{
            code = 'INVALID_QUANTITY'
            message = 'Quantity must be greater than zero'
            field = 'quantity'
            details = "Provided: $($body.quantity)"
        }
        Write-KrResponse $myError -StatusCode 400
        return
    }

    # Create order response
    $unitPrice = 99.99
    $totalPrice = [int]$body.quantity * $unitPrice
    $response = [OrderResponse] @{
        orderId = [System.Guid]::NewGuid().ToString()
        productId = [long]$body.productId
        quantity = [int]$body.quantity
        status = 'pending'
        totalPrice = $totalPrice
        createdAt = (Get-Date).ToUniversalTime().ToString('o')
        expectedDelivery = (Get-Date).AddDays(5).ToString('yyyy-MM-dd')
    }

    Write-KrResponse $response -StatusCode 201
}


<#
.SYNOPSIS
    Get an order by ID.
.DESCRIPTION
    Retrieves order details using the OrderResponse component.
.PARAMETER orderId
    The order ID to retrieve
.NOTES
    GET /orders/{orderId}: Get order
#>
function getOrder {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/orders/{orderId}')]
    [OpenApiResponseRefAttribute(StatusCode = '200', ReferenceId = 'OrderResponseDefault')]
    [OpenApiResponseRefAttribute(StatusCode = '400', ReferenceId = 'ErrorResponseDefault')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true)]
        [Guid]$orderId
    )

    # Validate UUID format
    if (-not [System.Guid]::TryParse($orderId, [ref][System.Guid]::Empty)) {
        $myError = [ErrorDetail] @{
            code = 'INVALID_ORDER_ID'
            message = 'Invalid order ID format'
            field = 'orderId'
            details = 'Order ID must be a valid UUID'
        }
        Write-KrResponse $myError -StatusCode 400
        return
    }

    # Mock order data
    $response = [OrderResponse]@{
        orderId = $orderId
        productId = 1
        quantity = 5
        status = 'processing'
        totalPrice = 499.95
        createdAt = (Get-Date).AddDays(-1).ToUniversalTime().ToString('o')
        expectedDelivery = (Get-Date).AddDays(4).ToString('yyyy-MM-dd')
    }

    Write-KrResponse $response -StatusCode 200
}


<#
.SYNOPSIS
    Update an existing order.
.DESCRIPTION
    Updates order details using the CreateOrderRequestBody component.
.PARAMETER orderId
    The order ID to update
.PARAMETER body
    Updated order data
.NOTES
    PUT /orders/{orderId}: Update order
#>
function updateOrder {
    [OpenApiPath(HttpVerb = 'put', Pattern = '/orders/{orderId}')]
    [OpenApiResponseRefAttribute(StatusCode = '200', ReferenceId = 'OrderResponseDefault')]
    [OpenApiResponseRefAttribute(StatusCode = '400', ReferenceId = 'ErrorResponseDefault')]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true, Example = 'a54a57ca-36f8-421b-a6b4-2e8f26858a4c')]
        [guid]$orderId,
        [OpenApiRequestBody(ContentType = ('application/json', 'application/xml', 'application/x-www-form-urlencoded'))]
        [CreateOrderRequestBody]$body
    )

    # Validate quantity if provided
    if ($body.quantity -and [int]$body.quantity -le 0) {
        $myError = [ErrorDetail] @{
            code = 'INVALID_QUANTITY'
            message = 'Quantity must be greater than zero'
            field = 'quantity'
        }
        Write-KrResponse $myError -StatusCode 400
        return
    }

    # Updated response
    $unitPrice = 99.99
    $quantity = $body.quantity -as [int] ? [int]$body.quantity : 5
    $response = [OrderResponse]@{
        orderId = $orderId
        productId = $body.productId -as [long] ? [long]$body.productId : 1
        quantity = $quantity
        status = 'processing'
        totalPrice = $quantity * $unitPrice
        createdAt = (Get-Date).AddDays(-1).ToUniversalTime().ToString('o')
        expectedDelivery = (Get-Date).AddDays(4).ToString('yyyy-MM-dd')
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

