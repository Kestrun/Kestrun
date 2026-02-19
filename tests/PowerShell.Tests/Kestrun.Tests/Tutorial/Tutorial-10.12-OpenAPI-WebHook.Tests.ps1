param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.12 WebHook' -Tag 'Tutorial', 'OpenApi', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.12-OpenAPI-WebHook.ps1'
    }

    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Webhook info endpoint returns event list' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/api/webhooks/info" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200

        $json = $result.Content | ConvertFrom-Json
        $json.available_webhooks | Should -Contain 'order.created'
        $json.available_webhooks | Should -Contain 'payment.completed'
        $json.available_webhooks | Should -Contain 'inventory.low_stock'
    }

    It 'Simulate order returns a realistic webhook payload' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/api/orders/simulate" -Method Post -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200

        $json = $result.Content | ConvertFrom-Json
        $json.message | Should -Be 'Order simulation created'
        $json.webhook_would_send.event_type | Should -Be 'order.created'
        $json.webhook_would_send.data.items.Count | Should -BeGreaterThan 0
    }

    It 'OpenAPI includes webhook definitions and component schemas' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $doc = $result.Content | ConvertFrom-Json

        # Component schemas registered
        $doc.components.schemas.OrderEventPayload | Should -Not -BeNullOrEmpty
        $doc.components.schemas.OrderData | Should -Not -BeNullOrEmpty
        $doc.components.schemas.OrderItem | Should -Not -BeNullOrEmpty
        $doc.components.schemas.PaymentEventPayload | Should -Not -BeNullOrEmpty
        $doc.components.schemas.PaymentData | Should -Not -BeNullOrEmpty
        $doc.components.schemas.InventoryEventPayload | Should -Not -BeNullOrEmpty
        $doc.components.schemas.InventoryData | Should -Not -BeNullOrEmpty

        $doc.components.schemas.OrderEventPayload.required | Should -Contain 'event_id'
        $doc.components.schemas.OrderEventPayload.required | Should -Contain 'data'

        # Webhooks exist and are wired to schemas
        $doc.webhooks | Should -Not -BeNullOrEmpty

        $webhookNames = @($doc.webhooks.PSObject.Properties.Name)
        $webhookNames.Count | Should -Be 5

        function Get-WebhookRequestSchemaRefs {
            param([Parameter(Mandatory)]$Webhook)

            $refs = @()
            $post = $Webhook.post
            if ($null -eq $post) { return $refs }
            $content = $post.requestBody.content
            if ($null -eq $content) { return $refs }

            foreach ($ct in $content.PSObject.Properties) {
                $schema = $ct.Value.schema
                if ($null -ne $schema -and ($schema.PSObject.Properties.Name -contains '$ref')) {
                    $refs += $schema.'$ref'
                }
            }
            return $refs
        }

        $webhookItems = foreach ($name in $webhookNames) {
            [pscustomobject]@{ Name = $name; Webhook = $doc.webhooks.$name }
        }

        foreach ($w in $webhookItems) {
            $w.Webhook.post | Should -Not -BeNullOrEmpty
            $w.Webhook.post.requestBody | Should -Not -BeNullOrEmpty
        }

        $orderRefsCount = @($webhookItems | Where-Object {
                (Get-WebhookRequestSchemaRefs -Webhook $_.Webhook) -contains '#/components/schemas/OrderEventPayload'
            }).Count
        $paymentRefsCount = @($webhookItems | Where-Object {
                (Get-WebhookRequestSchemaRefs -Webhook $_.Webhook) -contains '#/components/schemas/PaymentEventPayload'
            }).Count
        $inventoryRefsCount = @($webhookItems | Where-Object {
                (Get-WebhookRequestSchemaRefs -Webhook $_.Webhook) -contains '#/components/schemas/InventoryEventPayload'
            }).Count

        $orderRefsCount | Should -Be 2
        $paymentRefsCount | Should -Be 2
        $inventoryRefsCount | Should -Be 1

        # Demo endpoints are present in the OpenAPI paths
        $doc.paths.'/api/orders/simulate'.post | Should -Not -BeNullOrEmpty
        $doc.paths.'/api/payments/simulate'.post | Should -Not -BeNullOrEmpty
        $doc.paths.'/api/webhooks/info'.get | Should -Not -BeNullOrEmpty
    }

    It 'OpenAPI v3.0 output matches 10.12 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.0'
    }

    It 'OpenAPI v3.1 output matches 10.12 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.1'
    }

    It 'OpenAPI v3.2 output matches 10.12 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.2'
    }
}

