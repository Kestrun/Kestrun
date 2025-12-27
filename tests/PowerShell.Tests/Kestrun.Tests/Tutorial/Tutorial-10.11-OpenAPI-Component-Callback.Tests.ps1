param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.11 Component Callback' -Tag 'Tutorial', 'OpenApi', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.11-OpenAPI-Component-Callback.ps1'
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'Create payment returns 201 with a payment id' {
        $body = [ordered]@{
            amount = 129.99
            currency = 'USD'
            callbackUrls = [ordered]@{
                status = 'https://client.example.com/callbacks/payment-status'
                reservation = 'https://client.example.com/callbacks/reservation'
                shippingOrder = 'https://client.example.com/callbacks/shipping-order'
            }
        } | ConvertTo-Json -Depth 10

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/v1/payments" -Method Post -Body $body -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 201

        $json = $result.Content | ConvertFrom-Json
        $json.paymentId | Should -Match '^PAY-[0-9a-f]{8}$'
        $json.status | Should -Be 'pending'
    }

    It 'Get payment returns 200 for a created payment id' {
        $createBody = [ordered]@{
            amount = 25.5
            currency = 'USD'
            callbackUrls = [ordered]@{
                status = 'https://client.example.com/callbacks/payment-status'
                reservation = 'https://client.example.com/callbacks/reservation'
                shippingOrder = 'https://client.example.com/callbacks/shipping-order'
            }
        } | ConvertTo-Json -Depth 10

        $create = Invoke-WebRequest -Uri "$($script:instance.Url)/v1/payments" -Method Post -Body $createBody -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $create.StatusCode | Should -Be 201
        $paymentId = ($create.Content | ConvertFrom-Json).paymentId

        $get = Invoke-WebRequest -Uri "$($script:instance.Url)/v1/payments/$paymentId" -Method Get -SkipCertificateCheck -SkipHttpErrorCheck
        $get.StatusCode | Should -Be 200

        $json = $get.Content | ConvertFrom-Json
        $json.paymentId | Should -Be $paymentId
        $json.status | Should -Be 'pending'
    }

    It 'OpenAPI includes callback definitions, component schemas, and help-derived summary/description' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $doc = $result.Content | ConvertFrom-Json -AsHashtable

        # Component schemas registered
        $doc.components.schemas.CallbackUrls | Should -Not -BeNullOrEmpty
        $doc.components.schemas.CreatePaymentRequest | Should -Not -BeNullOrEmpty
        $doc.components.schemas.CreatePaymentResponse | Should -Not -BeNullOrEmpty
        $doc.components.schemas.PaymentStatusChangedEvent | Should -Not -BeNullOrEmpty
        $doc.components.schemas.ReservationCreatedEvent | Should -Not -BeNullOrEmpty
        $doc.components.schemas.ShippingOrderCreatedEvent | Should -Not -BeNullOrEmpty

        # Operation exists and exposes callbacks
        $post = $doc.paths['/v1/payments'].post
        $post | Should -Not -BeNullOrEmpty
        $post.callbacks | Should -Not -BeNullOrEmpty

        $callbackKeys = @($post.callbacks.Keys)
        $callbackKeys | Should -Contain 'paymentStatus'
        $callbackKeys | Should -Contain 'reservation'
        $callbackKeys | Should -Contain 'shippingOrder'

        @(
            (Get-CallbackRequestSchemaRefs -Doc $doc -Callback $post.callbacks.paymentStatus),
            (Get-CallbackRequestSchemaRefs -Doc $doc -Callback $post.callbacks.reservation),
            (Get-CallbackRequestSchemaRefs -Doc $doc -Callback $post.callbacks.shippingOrder)
        ) | Where-Object { $_ } | Should -Not -BeNullOrEmpty

        # Callback schemas wired
        (Get-CallbackRequestSchemaRefs -Doc $doc -Callback $post.callbacks.paymentStatus) | Should -Contain '#/components/schemas/PaymentStatusChangedEvent'
        (Get-CallbackRequestSchemaRefs -Doc $doc -Callback $post.callbacks.reservation) | Should -Contain '#/components/schemas/ReservationCreatedEvent'
        (Get-CallbackRequestSchemaRefs -Doc $doc -Callback $post.callbacks.shippingOrder) | Should -Contain '#/components/schemas/ShippingOrderCreatedEvent'

        # Comment-based help populates summary/description
        $post.summary | Should -Be 'Create a payment.'
        $post.description | Should -Match 'demonstrates how callbacks are documented'

        $get = $doc.paths['/v1/payments/{paymentId}'].get
        $get.summary | Should -Be 'Get payment.'
        $get.description | Should -Match 'retrieve a payment'
    }
}
