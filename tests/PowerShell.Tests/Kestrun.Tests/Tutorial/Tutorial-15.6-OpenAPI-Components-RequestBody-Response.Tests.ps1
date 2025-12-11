param()
Describe 'Example 15.6 OpenAPI Components RequestBody & Response' -Tag 'Tutorial', 'Slow' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1');
        $script:instance = Start-ExampleScript -Name '15.6-OpenAPI-Components-RequestBody-Response.ps1'
    }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

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
        $json.components.responses.'OrderResponseComponent-Default' | Should -Not -BeNullOrEmpty
    }
}
