<#
    Sample: OpenAPI Custom Error Handler
    Purpose: Demonstrate OpenAPI error schema + custom PowerShell runtime error response script.
    File:    10.26-OpenAPI-Custom-Error-Handler.ps1
#>
param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

New-KrServer -Name 'OpenAPI Custom Error Handler'
Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# OpenAPI metadata
Add-KrOpenApiInfo -Title 'Orders API - Custom Error Handler' -Version '1.0.0' -Description 'OpenAPI routes with a custom runtime error response script.'
Set-KrOpenApiErrorSchema -Name 'ApiError' -ContentType @('application/problem+json', 'application/json')

# Runtime custom error response script
Set-KrPowerShellErrorResponse -ScriptBlock {
    $payload = [ordered]@{
        status = $StatusCode
        title = 'Request failed'
        detail = $ErrorMessage
        path = [string]$Context.Request.Path
        timestamp = (Get-Date).ToUniversalTime().ToString('o')
    }

    Write-KrJsonResponse -InputObject $payload -StatusCode $StatusCode -ContentType 'application/problem+json'
}

[OpenApiSchemaComponent(RequiredProperties = ('id', 'sku', 'quantity'))]
class OrderDto {
    [OpenApiProperty(Description = 'Order identifier', Example = 1001)]
    [int]$id

    [OpenApiProperty(Description = 'Item sku', Example = 'SKU-RED-001')]
    [string]$sku

    [OpenApiProperty(Description = 'Quantity requested', Example = 2)]
    [int]$quantity
}

[OpenApiSchemaComponent(RequiredProperties = ('status', 'title', 'detail', 'path', 'timestamp'))]
class ApiError {
    [OpenApiProperty(Description = 'HTTP status code', Example = 500)]
    [int]$status

    [OpenApiProperty(Description = 'Short error summary', Example = 'Request failed')]
    [string]$title

    [OpenApiProperty(Description = 'Detailed error message', Example = 'Unhandled exception')]
    [string]$detail

    [OpenApiProperty(Description = 'Request path', Example = '/orders/13')]
    [string]$path

    [OpenApiProperty(Description = 'UTC timestamp', Example = '2026-02-17T10:11:12.0000000Z')]
    [OpenApiDateTime]$timestamp
}

[OpenApiSchemaComponent(RequiredProperties = ('sku', 'quantity'))]
class CreateOrderRequest {
    [OpenApiProperty(Description = 'Item sku', Example = 'SKU-RED-001')]
    [string]$sku

    [OpenApiProperty(Description = 'Quantity requested', Example = 2)]
    [int]$quantity
}

Enable-KrConfiguration

<#
.SYNOPSIS
    Get an order by ID.
.DESCRIPTION
    Returns a sample order payload and raises a runtime exception for order 13 to demonstrate
    the custom PowerShell error response handler on an OpenAPI route.
.PARAMETER orderId
    The order identifier from the route path.
#>
function getOrder {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/orders/{orderId}', Tags = ('orders'))]
    [OpenApiResponse(StatusCode = '200', Description = 'Order found', Schema = [OrderDto], ContentType = 'application/json')]
    [OpenApiResponse(StatusCode = '500', Description = 'Server error', Schema = [ApiError], ContentType = ('application/problem+json', 'application/json'))]
    param(
        [OpenApiParameter(In = [OaParameterLocation]::Path, Required = $true)]
        [int]$orderId
    )

    if ($orderId -eq 13) {
        throw [System.InvalidOperationException]::new('Order service unavailable for order 13.')
    }

    Write-KrResponse -InputObject @{
        id = $orderId
        sku = 'SKU-RED-001'
        quantity = 2
    } -StatusCode 200
}

<#
.SYNOPSIS
    Create a new order.
.DESCRIPTION
    Accepts an OpenAPI-described JSON request body and returns a created order. Invalid quantity
    raises a runtime exception to exercise the custom error response script.
.PARAMETER body
    The order creation payload bound from the request body.
#>
function createOrder {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/orders', Tags = ('orders'))]
    [OpenApiResponse(StatusCode = '201', Description = 'Order created', Schema = [OrderDto], ContentType = 'application/json')]
    [OpenApiResponse(StatusCode = '415', Description = 'Unsupported media type', Schema = [ApiError], ContentType = ('application/problem+json', 'application/json'))]
    [OpenApiResponse(StatusCode = '500', Description = 'Server error', Schema = [ApiError], ContentType = ('application/problem+json', 'application/json'))]
    param(
        [OpenApiRequestBody(Description = 'Order payload', Required = $true, ContentType = 'application/json')]
        [CreateOrderRequest]$body
    )

    if ($body.quantity -le 0) {
        throw [System.ArgumentOutOfRangeException]::new('quantity', 'Quantity must be greater than zero.')
    }

    Write-KrResponse -InputObject @{
        id = 2001
        sku = $body.sku
        quantity = $body.quantity
    } -StatusCode 201
}

Add-KrOpenApiRoute
Add-KrApiDocumentationRoute -DocumentType Swagger
Build-KrOpenApiDocument | Out-Null
Test-KrOpenApiDocument | Out-Null

Start-KrServer -CloseLogsOnExit
