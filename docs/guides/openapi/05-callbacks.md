---
title: Callbacks and Webhooks
parent: OpenAPI
nav_order: 5
---

# Callbacks and Webhooks

These are the most advanced OpenAPI features in this guide, so they live on their own page.

## Webhooks

OpenAPI **webhooks** document event notifications that your API may send to subscribers. They are **top-level** in the OpenAPI document under `webhooks`.

This is different from **callbacks**, which are tied to a specific operation.

> OpenAPI webhooks describe the payloads and contract, but do not define how consumers subscribe.
Subscription management is usually modeled as regular API endpoints such as `POST /subscriptions`.

### Define webhook payload schemas

```powershell
[OpenApiSchemaComponent(RequiredProperties = ('event_id', 'event_type', 'timestamp', 'data'))]
class OrderEventPayload {
    [OpenApiPropertyAttribute(Description = 'Unique identifier for this event', Format = 'uuid')]
    [string]$event_id

    [OpenApiPropertyAttribute(Description = 'Event type', Example = 'order.created')]
    [string]$event_type

    [OpenApiPropertyAttribute(Description = 'When the event occurred', Format = 'date-time')]
    [datetime]$timestamp

    [OpenApiPropertyAttribute(Description = 'Event payload')]
    [hashtable]$data
}
```

### Declare a webhook

```powershell
function orderCreatedWebhook {
    [OpenApiWebhook(HttpVerb = 'post', Pattern = '/webhooks/order.created')]
    param(
        [OpenApiRequestBody(Description = 'Order creation event payload')]
        [OrderEventPayload]$Body
    )
}
```

### Generate and view

1. Register OpenAPI generation: `Add-KrOpenApiRoute`
2. Build and validate: `Build-KrOpenApiDocument` and `Test-KrOpenApiDocument`
3. Fetch the document and confirm `webhooks` exists

## Callbacks

OpenAPI **callbacks** document requests that your API may send **as part of handling a specific operation**.
They live under `paths.{path}.{verb}.callbacks` and are typically driven by a callback URL supplied by the client.

### Typical pattern

1. The client calls an operation like `POST /v1/payments` and includes callback URLs in the request body
2. The API documents one or more callback requests using:
   - `[OpenApiCallback]` to declare the callback operation
   - `[OpenApiCallbackRef]` to attach it to the main operation under `callbacks`

In Kestrun, callbacks can be declared inline or as reusable callback components under `components.callbacks`.

At runtime, callback URLs are resolved from a URL template that can contain:

- A request-body runtime expression like `{$request.body#/callbackUrls/status}`
- Token placeholders like `{paymentId}` populated from callback function parameters

### Example: payment create + callback

```powershell
[OpenApiSchemaComponent(RequiredProperties = ('eventId', 'timestamp', 'paymentId', 'status'))]
class PaymentStatusChangedEvent {
    [OpenApiPropertyAttribute(Format = 'uuid')]
    [string]$eventId

    [OpenApiPropertyAttribute(Format = 'date-time')]
    [datetime]$timestamp

    [OpenApiPropertyAttribute(Example = 'PAY-12345678')]
    [string]$paymentId

    [ValidateSet('authorized', 'captured', 'failed')]
    [string]$status
}

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

function createPayment {
    [OpenApiPath(HttpVerb = 'post', Pattern = '/v1/payments')]
    [OpenApiResponse(StatusCode = '201', Description = 'Payment created')]
    [OpenApiCallbackRef(Key = 'paymentStatus', ReferenceId = 'paymentStatusCallback', Inline = $true)]
    param(
        [OpenApiRequestBody(ContentType = 'application/json')]
        [object]$body
    )

    Write-KrJsonResponse @{ paymentId = 'PAY-12345678'; status = 'pending' } -StatusCode 201
}
```

## Enabling callback automation

The callback metadata describes callback requests your API *may* send. To actually dispatch them at runtime from PowerShell,
enable Kestrun's callback automation middleware.

```powershell
Add-KrAddCallbacksAutomation
Enable-KrConfiguration
```

You can configure dispatch behavior directly:

```powershell
Add-KrAddCallbacksAutomation -DefaultTimeout 30 -MaxAttempts 5 -BaseDelay 2 -MaxDelay 60
```

Or with a typed options object:

```powershell
$opts = [Kestrun.Callback.CallbackDispatchOptions]::new()
$opts.DefaultTimeout = [TimeSpan]::FromSeconds(30)
$opts.MaxAttempts = 5
$opts.BaseDelay = [TimeSpan]::FromSeconds(2)
$opts.MaxDelay = [TimeSpan]::FromSeconds(60)

Add-KrAddCallbacksAutomation -Options $opts
```

## How callback URLs are resolved

The callback URL is built by combining:

1. The client-provided value referenced by the callback `Expression`
2. The callback `Pattern`
3. Token values such as `{paymentId}` taken from callback function parameters

If the resolved URL is absolute, it is used as-is. If it is relative, Kestrun combines it with a default base URI derived from the current request.

> **Warning:** When callback URLs come from client-supplied data, using them as-is can introduce SSRF risk.
In production, validate and constrain callback targets with a strict host/protocol allowlist or a custom resolver.

## Using callbacks inside an operation

A common pattern is:

1. Accept callback URLs in the operation request body
2. Perform the main work
3. Invoke one or more callback functions with the required path tokens and body payload

Client-side request body example:

```powershell
$body = @{
    amount = 129.99
    currency = 'USD'
    callbackUrls = @{
        status = 'https://client.example.com/callbacks/payment-status'
        reservation = 'https://client.example.com/callbacks/reservation'
        shippingOrder = 'https://client.example.com/callbacks/shipping-order'
    }
} | ConvertTo-Json -Depth 10
```

Server-side callback invocation example:

```powershell
paymentStatusCallback -PaymentId $paymentId -Body $paymentStatusChangedEvent
reservationCallback -OrderId $orderId -Body $reservationEvent
shippingOrderCallback -OrderId $orderId -Body $shippingOrderEvent
```
