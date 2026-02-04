param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.11 Component Callback' -Tag 'Tutorial', 'OpenApi', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.11-OpenAPI-Component-Callback.ps1'
    }

    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
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

    It 'Callback automation dispatches callbacks to provided URLs' {
        # Point the callbackUrls back at this same local server.
        # The callback URL template appends the callback pattern (e.g. /v1/payments/{paymentId}/status)
        # to these base URLs.
        $base = $script:instance.Url

        $body = [ordered]@{
            amount = 129.99
            currency = 'USD'
            callbackUrls = [ordered]@{
                status = "$base/callbacks/payment-status"
                reservation = "$base/callbacks/reservation"
                shippingOrder = "$base/callbacks/shipping-order"
            }
        } | ConvertTo-Json -Depth 10

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/v1/payments" -Method Post -Body $body -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 201

        # Callback dispatch happens during the request; allow a small window for the internal HTTP calls
        # and the receiver routes to write to stdout.
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        $stdout = ''
        do {
            Start-Sleep -Milliseconds 250
            if (Test-Path $script:instance.StdOut) {
                $stdout = Get-Content -Path $script:instance.StdOut -Raw -ErrorAction SilentlyContinue
            }
        } while (
            [DateTime]::UtcNow -lt $deadline -and
            (
                $stdout -notmatch 'Created CallbackRequest:.*CallbackId=paymentStatus' -or
                $stdout -notmatch 'Created CallbackRequest:.*CallbackId=reservation' -or
                $stdout -notmatch 'Created CallbackRequest:.*CallbackId=shippingOrder' -or
                $stdout -notmatch 'Received payment status callback' -or
                $stdout -notmatch 'Received reservation callback' -or
                $stdout -notmatch 'Received shipping order callback'
            )
        )

        $stdout | Should -Match 'Created CallbackRequest:.*CallbackId=paymentStatus'
        $stdout | Should -Match 'Created CallbackRequest:.*CallbackId=reservation'
        $stdout | Should -Match 'Created CallbackRequest:.*CallbackId=shippingOrder'

        $stdout | Should -Match 'Received payment status callback'
        $stdout | Should -Match 'Received reservation callback'
        $stdout | Should -Match 'Received shipping order callback'
    }

    It 'OpenAPI output matches 10.11 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance
    }
}
