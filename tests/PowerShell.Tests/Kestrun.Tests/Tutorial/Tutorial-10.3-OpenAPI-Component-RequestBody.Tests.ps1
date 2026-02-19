param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.3 OpenAPI Component RequestBody' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.3-OpenAPI-Component-RequestBody.ps1' }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Create Product (POST)' {
        $body = @{
            productName = 'New Laptop'
            price = 1500.00
            description = 'Fast'
            stock = 10
        } | ConvertTo-Json

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/products" -Method Post -Body $body -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 201
        $json = $result.Content | ConvertFrom-Json
        $json.productName | Should -Be 'New Laptop'
    }

    It 'Update Product (PUT)' {
        $body = @{
            productName = 'Updated Laptop'
            price = 1400.00
            stock = 5
        } | ConvertTo-Json

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/products/1" -Method Put -Body $body -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.productName | Should -Be 'Updated Laptop'
    }

    It 'Check OpenAPI RequestBodies' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.components.requestBodies.CreateProductRequest | Should -Not -BeNullOrEmpty
        $json.components.requestBodies.UpdateProductRequest | Should -Not -BeNullOrEmpty
    }

    It 'OpenAPI v3.0 output matches 10.3 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.0'
    }

    It 'OpenAPI v3.1 output matches 10.3 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.1'
    }

    It 'OpenAPI v3.2 output matches 10.3 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.2'
    }
}
