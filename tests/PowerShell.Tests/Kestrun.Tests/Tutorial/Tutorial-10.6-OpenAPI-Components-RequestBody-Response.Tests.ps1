param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.6 OpenAPI Components RequestBody & Response' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.6-OpenAPI-Components-RequestBody-Response.ps1'
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Create Order (POST)' {
        $body = @{
            productId = 101
            quantity = 2
            customerEmail = 'test@example.com'
            shippingAddress = '123 Main St'
        } | ConvertTo-Json

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/orders" -Method Post -Body $body -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 201
        $json = $result.Content | ConvertFrom-Json
        $json.productId | Should -Be 101
        $json.status | Should -Be 'pending'
        $script:orderId = $json.orderId
    }

    It 'Get Order (GET)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/orders/$($script:orderId)" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.orderId | Should -Be $script:orderId
    }

    It 'Check OpenAPI Components' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $json = $result.Content | ConvertFrom-Json
        $json.components.requestBodies.CreateOrderRequestBody | Should -Not -BeNullOrEmpty
        $json.components.responses.OrderResponseDefault | Should -Not -BeNullOrEmpty
    }

    It 'Check OpenAPI Component Extensions' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $json = $result.Content | ConvertFrom-Json

        $json.components.requestBodies.CreateOrderRequestBody.'x-kestrun-demo'.kind | Should -Be 'request'
        $json.components.requestBodies.CreateOrderRequestBody.'x-kestrun-demo'.containsPii | Should -BeTrue

        $json.components.responses.OrderResponseDefault.'x-kestrun-demo'.domain | Should -Be 'orders'
        $json.components.responses.OrderResponseDefault.'x-kestrun-demo'.kind | Should -Be 'success'

        $json.components.responses.ErrorResponseDefault.'x-kestrun-demo'.kind | Should -Be 'error'
        $json.components.responses.ErrorResponseDefault.'x-kestrun-demo'.retryable | Should -BeFalse
    }

    It 'OpenAPI v3.0 output matches 10.6 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.0'
    }

    It 'OpenAPI v3.1 output matches 10.6 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.1'
    }

    It 'OpenAPI v3.2 output matches 10.6 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.2'
    }
}
