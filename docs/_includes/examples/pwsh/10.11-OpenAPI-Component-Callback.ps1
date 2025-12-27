<#
    Sample: OpenAPI Callback Components
    Purpose: Demonstrate OpenAPI callbacks for async event notifications.
    File:    10.11-OpenAPI-Component-Callback.ps1
    Notes:   Shows a realistic payment flow where the client supplies callback URLs
             and the provider documents the callback requests it may send.
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
    -Description 'Demonstrates OpenAPI callbacks (async notifications) using component references and inline callbacks.'

Add-KrOpenApiContact -Email 'support@example.com'
# Add Server info
Add-KrOpenApiServer -Url "http://localhost:$Port" -Description 'Local Server'

# =========================================================
#                 COMPONENT SCHEMAS
# =========================================================

[OpenApiSchemaComponent(Description = 'Callback URLs supplied by the client for async notifications.', Required = ('status', 'reservation', 'shippingOrder'))]
class CallbackUrls {
    [OpenApiPropertyAttribute(Description = 'Callback URL for payment status updates.', Format = 'uri', Example = 'https://client.example.com/callbacks/payment-status')]
    [string]$status

    [OpenApiPropertyAttribute(Description = 'Callback URL for reservation created events.', Format = 'uri', Example = 'https://client.example.com/callbacks/reservation')]
    [string]$reservation

    [OpenApiPropertyAttribute(Description = 'Callback URL for shipping order created events.', Format = 'uri', Example = 'https://client.example.com/callbacks/shipping-order')]
    [string]$shippingOrder
}

[OpenApiSchemaComponent(Description = 'Request payload to create a payment. Includes callback URLs for async updates.', Required = ('amount', 'currency', 'callbackUrls'))]
class CreatePaymentRequest {
    [OpenApiPropertyAttribute(Description = 'Payment amount', Example = 129.99, Minimum = 0)]
    [double]$amount

    [OpenApiPropertyAttribute(Description = 'Currency code', Example = 'USD')]
    [string]$currency

    [OpenApiPropertyAttribute(Description = 'Client-provided callback URLs')]
    [CallbackUrls]$callbackUrls
}

[OpenApiSchemaComponent(Description = 'Response returned after creating a payment.', Required = ('paymentId', 'status'))]
class CreatePaymentResponse {
    [OpenApiPropertyAttribute(Description = 'Payment identifier', Example = 'PAY-12345678')]
    [string]$paymentId

    [OpenApiPropertyAttribute(Description = 'Current payment status', Example = 'pending')]
    [ValidateSet('pending', 'authorized', 'captured', 'failed')]
    [string]$status
}

[OpenApiSchemaComponent(Description = 'Callback event payload for payment status changes.', Required = ('eventId', 'timestamp', 'paymentId', 'status'))]
class PaymentStatusChangedEvent {
    [OpenApiPropertyAttribute(Description = 'Event identifier', Format = 'uuid')]
    [string]$eventId

    [OpenApiPropertyAttribute(Description = 'When the event occurred', Format = 'date-time')]
    [datetime]$timestamp

    [OpenApiPropertyAttribute(Description = 'Payment identifier', Example = 'PAY-12345678')]
    [string]$paymentId

    [OpenApiPropertyAttribute(Description = 'New status', Example = 'authorized')]
    [ValidateSet('authorized', 'captured', 'failed')]
    [string]$status
}

[OpenApiSchemaComponent(Description = 'Callback event payload for reservation creation.', Required = ('eventId', 'timestamp', 'orderId', 'reservationId'))]
class ReservationCreatedEvent {
    [OpenApiPropertyAttribute(Description = 'Event identifier', Format = 'uuid')]
    [string]$eventId

    [OpenApiPropertyAttribute(Description = 'When the event occurred', Format = 'date-time')]
    [datetime]$timestamp

    [OpenApiPropertyAttribute(Description = 'Order identifier', Example = 'ORD-98765432')]
    [string]$orderId

    [OpenApiPropertyAttribute(Description = 'Reservation identifier', Example = 'RSV-12345678')]
    [string]$reservationId
}

[OpenApiSchemaComponent(Description = 'Callback event payload for shipping order creation.', Required = ('eventId', 'timestamp', 'orderId', 'shippingOrderId'))]
class ShippingOrderCreatedEvent {
    [OpenApiPropertyAttribute(Description = 'Event identifier', Format = 'uuid')]
    [string]$eventId

    [OpenApiPropertyAttribute(Description = 'When the event occurred', Format = 'date-time')]
    [datetime]$timestamp

    [OpenApiPropertyAttribute(Description = 'Order identifier', Example = 'ORD-98765432')]
    [string]$orderId

    [OpenApiPropertyAttribute(Description = 'Shipping order identifier', Example = 'SHP-12345678')]
    [string]$shippingOrderId
}

# =========================================================
#                 ROUTES / OPERATIONS
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

# Callback component definitions for payment-related events

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
        [OpenApiParameter(In = 'path', Required = $true)]
        [string]$paymentId,

        [OpenApiRequestBody(ContentType = 'application/json')]
        [PaymentStatusChangedEvent]$Body
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
        [OpenApiParameter(In = 'path', Required = $true)]
        [string]$orderId,

        [OpenApiRequestBody(ContentType = 'application/json')]
        [ReservationCreatedEvent]$Body
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
        [OpenApiParameter(In = 'path', Required = $true)]
        [string]$orderId,

        [OpenApiRequestBody(ContentType = 'application/json')]
        [ShippingOrderCreatedEvent]$Body
    )
}

<#
.SYNOPSIS
    Create a payment.
.DESCRIPTION
    Creates a new payment and demonstrates how callbacks are documented.
    The request includes callback URLs used by the provider to notify the consumer asynchronously.
.PARAMETER body
    Payment creation request including callback URLs.
#>
function createPayment {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/v1/payments')]
    [OpenApiResponse(StatusCode = '201', Description = 'Payment created', Schema = [CreatePaymentResponse], ContentType = 'application/json')]
    [OpenApiResponse(StatusCode = '400', Description = 'Invalid input')]
    [OpenApiCallbackRef(Key = 'paymentStatus', ReferenceId = 'paymentStatusCallback', Inline = $true)]
    [OpenApiCallbackRef(Key = 'reservation', ReferenceId = 'reservationCallback')]
    [OpenApiCallbackRef(Key = 'shippingOrder', ReferenceId = 'shippingOrderCallback')]
    param(
        [OpenApiRequestBody(ContentType = 'application/json')]
        [CreatePaymentRequest]$body
    )

    if (-not $body -or -not $body.amount -or -not $body.currency -or -not $body.callbackUrls) {
        Write-KrJsonResponse @{ error = 'amount, currency, and callbackUrls are required' } -StatusCode 400
        return
    }

    $paymentId = 'PAY-' + ([guid]::NewGuid().ToString('N').Substring(0, 8))
    Write-KrJsonResponse ([ordered]@{
            paymentId = $paymentId
            status = 'pending'
        }) -StatusCode 201
}

<#
.SYNOPSIS
    Get payment.
.DESCRIPTION
    Simple endpoint to retrieve a payment resource.
.PARAMETER paymentId
    The payment identifier.
#>
function getPayment {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/v1/payments/{paymentId}')]
    [OpenApiResponse(StatusCode = '200', Description = 'Payment found', Schema = [CreatePaymentResponse], ContentType = 'application/json')]
    [OpenApiResponse(StatusCode = '404', Description = 'Payment not found')]
    param(
        [OpenApiParameter(In = 'path', Required = $true)]
        [string]$paymentId
    )

    Write-KrJsonResponse ([ordered]@{
            paymentId = $paymentId
            status = 'pending'
        }) -StatusCode 200
}


# =========================================================
#                OPENAPI DOC ROUTE / BUILD
# =========================================================

Add-KrOpenApiRoute  # Default pattern '/openapi/{version}/openapi.{format}'

Build-KrOpenApiDocument
Test-KrOpenApiDocument

# =========================================================
#                      RUN SERVER
# =========================================================

Start-KrServer -Server $srv -CloseLogsOnExit
