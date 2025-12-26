<#
    Sample: OpenAPI WebHook
    Purpose: Demonstrate webhook components for e-commerce event notifications.
    File:    10.12-OpenAPI-WebHook.ps1
    Notes:   Shows realistic webhook scenarios with order, payment, and inventory events.
             Webhooks define the structure of notifications your API sends to subscribers.
#>

param(
    [int]$Port = 5000,
    [IPAddress]$IPAddress = [IPAddress]::Loopback
)

# --- Logging / Server ---

New-KrLogger | Add-KrSinkConsole |
    Set-KrLoggerLevel -Value Debug |
    Register-KrLogger -Name 'console' -SetAsDefault

$srv = New-KrServer -Name 'E-Commerce Webhook API' -PassThru

Add-KrEndpoint -Port $Port -IPAddress $IPAddress

# =========================================================
#                 TOP-LEVEL OPENAPI
# =========================================================

Add-KrOpenApiInfo -Title 'E-Commerce Webhook API' `
    -Version '1.0.0' `
    -Description @'
E-Commerce API demonstrating webhook notifications for orders, payments, and inventory events.
Webhooks allow your subscribers to receive real-time notifications when events occur in your system.
'@

Add-KrOpenApiContact -Email 'api-support@example.com' -Name 'API Support Team'
Add-KrOpenApiServer -Url "http://$($IPAddress):$Port" -Description 'Local Development Server'

# =========================================================
#              WEBHOOK SCHEMAS (COMPONENTS)
# =========================================================

# Order Event Payload
[OpenApiSchemaComponent(
    Description = 'Webhook payload for order-related events.',
    Required = ('event_id', 'event_type', 'timestamp', 'data')
)]
class OrderEventPayload {
    [OpenApiProperty(Description = 'Unique identifier for this event', Format = 'uuid')]
    [string]$event_id

    [OpenApiProperty(Description = 'Type of order event', Example = 'order.created')]
    [string]$event_type

    [OpenApiProperty(Description = 'When the event occurred', Format = 'date-time')]
    [datetime]$timestamp

    [OpenApiProperty(Description = 'Order data payload')]
    [OrderData]$data
}

[OpenApiSchemaComponent(
    Description = 'Order details for webhook events.',
    Required = ('order_id', 'customer_id', 'status', 'total_amount', 'currency', 'items')
)]
class OrderData {
    [OpenApiProperty(Description = 'Unique order identifier', Example = 'ORD-abc12345')]
    [string]$order_id

    [OpenApiProperty(Description = 'Customer identifier', Example = 'CUST-12345')]
    [string]$customer_id

    [OpenApiProperty(Description = 'Current order status', Example = 'pending')]
    [ValidateSet('pending', 'processing', 'shipped', 'delivered', 'cancelled', 'refunded')]
    [string]$status

    [OpenApiProperty(Description = 'Total order amount', Example = '129.99')]
    [decimal]$total_amount

    [OpenApiProperty(Description = 'Currency code', Example = 'USD')]
    [string]$currency

    [OpenApiProperty(Description = 'List of items in the order')]
    [OrderItem[]]$items
}

[OpenApiSchemaComponent(
    Description = 'Individual item in an order.',
    Required = ('product_id', 'product_name', 'quantity', 'unit_price')
)]
class OrderItem {
    [OpenApiProperty(Description = 'Product identifier', Example = 'PROD-001')]
    [string]$product_id

    [OpenApiProperty(Description = 'Product name', Example = 'Wireless Headphones')]
    [string]$product_name

    [OpenApiProperty(Description = 'Quantity ordered', Example = '1')]
    [int]$quantity

    [OpenApiProperty(Description = 'Price per unit', Example = '79.99')]
    [decimal]$unit_price
}

# Payment Event Payload
[OpenApiSchemaComponent(
    Description = 'Webhook payload for payment-related events.',
    Required = ('event_id', 'event_type', 'timestamp', 'data')
)]
class PaymentEventPayload {
    [OpenApiProperty(Description = 'Unique identifier for this event', Format = 'uuid')]
    [string]$event_id

    [OpenApiProperty(Description = 'Type of payment event', Example = 'payment.completed')]
    [string]$event_type

    [OpenApiProperty(Description = 'When the event occurred', Format = 'date-time')]
    [datetime]$timestamp

    [OpenApiProperty(Description = 'Payment data payload')]
    [PaymentData]$data
}

[OpenApiSchemaComponent(
    Description = 'Payment transaction details for webhook events.',
    Required = ('payment_id', 'order_id', 'customer_id', 'amount', 'currency', 'status', 'payment_method', 'transaction_id')
)]
class PaymentData {
    [OpenApiProperty(Description = 'Unique payment identifier', Example = 'PAY-xyz98765')]
    [string]$payment_id

    [OpenApiProperty(Description = 'Associated order identifier', Example = 'ORD-abc12345')]
    [string]$order_id

    [OpenApiProperty(Description = 'Customer identifier', Example = 'CUST-12345')]
    [string]$customer_id

    [OpenApiProperty(Description = 'Payment amount', Example = '129.99')]
    [decimal]$amount

    [OpenApiProperty(Description = 'Currency code', Example = 'USD')]
    [string]$currency

    [OpenApiProperty(Description = 'Payment status', Example = 'completed')]
    [ValidateSet('pending', 'completed', 'failed', 'refunded')]
    [string]$status

    [OpenApiProperty(Description = 'Payment method used', Example = 'credit_card')]
    [string]$payment_method

    [OpenApiProperty(Description = 'Transaction ID from payment processor', Example = 'TXN-1a2b3c4d')]
    [string]$transaction_id
}

# Inventory Event Payload
[OpenApiSchemaComponent(
    Description = 'Webhook payload for inventory-related events.',
    Required = ('event_id', 'event_type', 'timestamp', 'data')
)]
class InventoryEventPayload {
    [OpenApiProperty(Description = 'Unique identifier for this event', Format = 'uuid')]
    [string]$event_id

    [OpenApiProperty(Description = 'Type of inventory event', Example = 'inventory.low_stock')]
    [string]$event_type

    [OpenApiProperty(Description = 'When the event occurred', Format = 'date-time')]
    [datetime]$timestamp

    [OpenApiProperty(Description = 'Inventory data payload')]
    [InventoryData]$data
}

[OpenApiSchemaComponent(
    Description = 'Inventory level details for webhook events.',
    Required = ('product_id', 'product_name', 'sku', 'current_stock', 'threshold')
)]
class InventoryData {
    [OpenApiProperty(Description = 'Product identifier', Example = 'PROD-001')]
    [string]$product_id

    [OpenApiProperty(Description = 'Product name', Example = 'Wireless Headphones')]
    [string]$product_name

    [OpenApiProperty(Description = 'Stock keeping unit', Example = 'WH-BLK-001')]
    [string]$sku

    [OpenApiProperty(Description = 'Current stock level', Example = '5')]
    [int]$current_stock

    [OpenApiProperty(Description = 'Low stock threshold', Example = '10')]
    [int]$threshold

    [OpenApiProperty(Description = 'Warehouse location', Example = 'Warehouse A, Aisle 3, Shelf 2')]
    [string]$warehouse_location
}

# =========================================================
#                    WEBHOOK DEFINITIONS
# =========================================================

<#
.SYNOPSIS
    Order Created Webhook
.DESCRIPTION
    Notifies subscribers when a new order is created in the system.
    Subscribers should implement this endpoint to receive order creation events.
.PARAMETER Body
    The order event payload containing order details
#>
function orderCreatedWebhook {
    [OpenApiWebhook(HttpVerb = 'post', Pattern = '/webhooks/order.created')]
    param(
        [OpenApiRequestBody(Description = 'Order creation event payload')]
        [OrderEventPayload]$Body
    )
}

<#
.SYNOPSIS
    Order Status Updated Webhook
.DESCRIPTION
    Notifies subscribers when an order status changes (e.g., shipped, delivered, cancelled).
    Common statuses: pending, processing, shipped, delivered, cancelled, refunded.
.PARAMETER Body
    The order event payload with updated status information
#>
function orderStatusUpdatedWebhook {
    [OpenApiWebhook(HttpVerb = 'post', Pattern = '/webhooks/order.status_updated')]
    param(
        [OpenApiRequestBody(Description = 'Order status update event payload')]
        [OrderEventPayload]$Body
    )
}

<#
.SYNOPSIS
    Payment Completed Webhook
.DESCRIPTION
    Notifies subscribers when a payment transaction is successfully completed.
    This event is triggered after payment authorization and capture.
.PARAMETER Body
    The payment event payload containing transaction details
#>
function paymentCompletedWebhook {
    [OpenApiWebhook(HttpVerb = 'post', Pattern = '/webhooks/payment.completed')]
    param(
        [OpenApiRequestBody(Description = 'Payment completion event payload')]
        [PaymentEventPayload]$Body
    )
}

<#
.SYNOPSIS
    Payment Failed Webhook
.DESCRIPTION
    Notifies subscribers when a payment transaction fails.
    Includes failure reason and recommended next steps.
.PARAMETER Body
    The payment event payload with failure details
#>
function paymentFailedWebhook {
    [OpenApiWebhook(HttpVerb = 'post', Pattern = '/webhooks/payment.failed')]
    param(
        [OpenApiRequestBody(Description = 'Payment failure event payload')]
        [PaymentEventPayload]$Body
    )
}

<#
.SYNOPSIS
    Inventory Low Stock Alert Webhook
.DESCRIPTION
    Notifies subscribers when product inventory falls below the configured threshold.
    Allows automated reordering or stock management workflows.
.PARAMETER Body
    The inventory event payload with current stock levels
#>
function inventoryLowStockWebhook {
    [OpenApiWebhook(HttpVerb = 'post', Pattern = '/webhooks/inventory.low_stock')]
    param(
        [OpenApiRequestBody(Description = 'Low stock alert event payload')]
        [InventoryEventPayload]$Body
    )
}

# =========================================================
#              MANAGEMENT ROUTES (FOR DEMO)
# =========================================================

Enable-KrConfiguration

Add-KrApiDocumentationRoute -DocumentType Swagger
Add-KrApiDocumentationRoute -DocumentType Redoc

<#
.SYNOPSIS
    Simulate Order Creation
.DESCRIPTION
    Creates a test order and demonstrates what webhook payload would be sent.
    In a real system, this would trigger the webhook to all registered subscribers.
#>
function simulateOrderCreation {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/orders/simulate')]
    param()

    $webhookPayload = @{
        event_id = [guid]::NewGuid().ToString()
        event_type = 'order.created'
        timestamp = [datetime]::UtcNow
        data = @{
            order_id = 'ORD-' + [guid]::NewGuid().ToString().Substring(0, 8)
            customer_id = 'CUST-12345'
            status = 'pending'
            total_amount = 129.99
            currency = 'USD'
            items = @(
                @{
                    product_id = 'PROD-001'
                    product_name = 'Wireless Headphones'
                    quantity = 1
                    unit_price = 79.99
                },
                @{
                    product_id = 'PROD-002'
                    product_name = 'USB-C Cable'
                    quantity = 2
                    unit_price = 25.00
                }
            )
        }
    }

    Write-KrJsonResponse @{
        message = 'Order simulation created'
        webhook_would_send = $webhookPayload
        subscribers_notified = 3
    } -StatusCode 200
}

<#
.SYNOPSIS
    Simulate Payment Event
.DESCRIPTION
    Creates a test payment event and shows the webhook payload structure.
#>
function simulatePaymentEvent {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/api/payments/simulate')]
    param()

    $webhookPayload = @{
        event_id = [guid]::NewGuid().ToString()
        event_type = 'payment.completed'
        timestamp = [datetime]::UtcNow
        data = @{
            payment_id = 'PAY-' + [guid]::NewGuid().ToString().Substring(0, 8)
            order_id = 'ORD-' + [guid]::NewGuid().ToString().Substring(0, 8)
            customer_id = 'CUST-12345'
            amount = 129.99
            currency = 'USD'
            status = 'completed'
            payment_method = 'credit_card'
            transaction_id = 'TXN-' + [guid]::NewGuid().ToString()
        }
    }

    Write-KrJsonResponse @{
        message = 'Payment simulation completed'
        webhook_would_send = $webhookPayload
        subscribers_notified = 2
    } -StatusCode 200
}

<#
.SYNOPSIS
    Get Webhook Documentation
.DESCRIPTION
    Returns information about available webhooks and how to subscribe.
#>
function getWebhookInfo {
    [OpenApiPath(HttpVerb = 'get', Pattern = '/api/webhooks/info')]
    param()

    Write-KrJsonResponse @{
        available_webhooks = @(
            'order.created',
            'order.status_updated',
            'payment.completed',
            'payment.failed',
            'inventory.low_stock'
        )
        documentation = 'See /openapi.json for webhook schemas'
        how_to_subscribe = 'POST to /api/webhooks/subscribe with your endpoint URL and event types'
    } -StatusCode 200
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
