<#
    Sample: OpenAPI Callback Components
    Purpose: Demonstrate reusable request header components with multiple content types.
    File:    10.11-OpenAPI-Component-Callback.ps1
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

$srv = New-KrServer -Name 'OpenAPI Hello World' -PassThru

Add-KrEndpoint -Port $Port -IPAddress $IPAddress
# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'Hello World API' `
    -Version '1.0.0' `
    -Description 'A simple OpenAPI 3.1 example with a single endpoint.'

Add-KrOpenApiContact -Email 'support@example.com'
# Add Server info
Add-KrOpenApiServer -Url "http://$($IPAddress):$Port" -Description 'Local Server'

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

# Simple GET endpoint with a text response

<#
.SYNOPSIS
    Payment Status Callback (component)
.DESCRIPTION
    Provider calls the consumer back when a payment status changes.
.PARAMETER paymentId
    The ID of the payment
.PARAMETER Body
    The callback event payload
#>
function paymentStatusCallback {
    [OpenApiCallback(
        Expression = '$request.body#/callbackUrls/status',
        HttpVerb = 'post',
        Pattern = '/v1/payments/{paymentId}/status',
        Inline = $true
    )]
    param(
        [OpenApiParameter( In = 'path', Required = $true)]
        [string] $paymentId,

        [OpenApiRequestBody()]
        [string] $Body
    )
}


<#
.SYNOPSIS
    Reservation Callback (component)
.DESCRIPTION
    Provider calls the consumer back when a reservation is made.
.PARAMETER orderId
    The ID of the order
.PARAMETER Body
    The callback event payload
#>
function reservationCallback {
    [OpenApiCallback(
        Expression = '$request.body#/callbackUrls/reservation',
        HttpVerb = 'post',
        Pattern = '/v1/orders/{orderId}/reservation'
    )]
    param(
        [OpenApiParameter( In = 'path', Required = $true)]
        [string] $orderId,

        [OpenApiRequestBody()]
        [string] $Body
    )
}

<#
.SYNOPSIS
    Shipping Order Callback (component)
.DESCRIPTION
    Provider calls the consumer back when a shipping order is created.
.PARAMETER orderId
    The ID of the order
.PARAMETER Body
    The callback event payload
#>
function shippingOrderCallback {
    [OpenApiCallback(
        Expression = '$request.body#/callbackUrls/shippingOrder',
        HttpVerb = 'post',
        Pattern = '/v1/orders/{orderId}/shippingOrder'
    )]
    param(
        [OpenApiParameter( In = 'path', Required = $true)]
        [string] $orderId,

        [OpenApiRequestBody()]
        [string] $Body
    )
}
<#
.SYNOPSIS
    Get greeting message.
.DESCRIPTION
    Returns a simple greeting message.
#>
function getGreeting {
    [OpenApiPath(HttpVerb = 'get' , Pattern = '/greeting' )]
    [OpenApiCallbackRef( Key = 'paymentStatusCallback', ReferenceId = 'paymentStatusCallback')]
    [OpenApiCallbackRef( Key = 'reservationCallback', ReferenceId = 'reservationCallback')]
    [OpenApiCallbackRef( Key = 'shippingOrderCallback', ReferenceId = 'shippingOrderCallback', Inline = $true)]
    param()
    Write-KrTextResponse -Text 'Hello, World!' -StatusCode 200
}


# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute  # Default pattern '/openapi/{version}/openapi.{format}'

# =========================================================
#                      RUN SERVER
# =========================================================

Start-KrServer -Server $srv -CloseLogsOnExit
