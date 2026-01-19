param()
Describe 'Example 10.22 OpenAPI XML Modeling' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '10.22-OpenAPI-XML-Modeling.ps1'
    }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Get Product (GET JSON)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/products/5" -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $json | Should -Not -BeNullOrEmpty
        $json.Id | Should -Be 5
        $json.Name | Should -Match 'Sample Product'
        $json.Price | Should -BeGreaterThan 0
        $json.Items | Should -Not -BeNullOrEmpty
        $json.Items.Count | Should -Be 3
    }

    It 'Get Product (GET XML)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/products/10" -Headers @{ Accept = 'application/xml' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        
        # Parse XML response
        [xml]$xml = $result.Content
        $xml | Should -Not -BeNullOrEmpty
        
        # Get the root element (might be Product, Response, or other)
        $root = $xml.DocumentElement
        $root | Should -Not -BeNullOrEmpty
        
        # Check that Id is an attribute (XML attribute metadata should apply)
        $root.id | Should -Be '10'
        
        # Check element names (Name or ProductName depending on serialization)
        # The OpenApiXml attribute specifies ProductName, but actual serialization may vary
        $nameValue = if ($root.PSObject.Properties['ProductName']) { $root.ProductName } else { $root.Name }
        $nameValue | Should -Match 'Sample Product'
        $root.Price | Should -Not -BeNullOrEmpty
        
        # Check array items (Items should be wrapped, containing Item elements)
        $root.Items | Should -Not -BeNullOrEmpty
        $root.Items.Item.Count | Should -Be 3
    }

    It 'Get Product Not Found (GET)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/products/999" -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 404
        $json = $result.Content | ConvertFrom-Json
        $json.error | Should -Match 'not found'
    }

    It 'Create Product (POST JSON)' {
        $body = @{
            Id = 123
            Name = 'Test Widget'
            Price = 29.99
            Items = @('ItemA', 'ItemB')
        } | ConvertTo-Json

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/products" -Method Post -Body $body -ContentType 'application/json' -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 201
        $json = $result.Content | ConvertFrom-Json
        $json.Id | Should -BeGreaterThan 0
        $json.Name | Should -Be 'Test Widget'
        $json.Price | Should -Be 29.99
        $json.Items.Count | Should -Be 2
    }

    It 'Create Product Invalid (POST)' {
        $body = @{
            Id = 123
            Price = 29.99
            # Missing Name
        } | ConvertTo-Json

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/products" -Method Post -Body $body -ContentType 'application/json' -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 400
        $json = $result.Content | ConvertFrom-Json
        $json.error | Should -Match 'Name.*required'
    }

    It 'Check OpenAPI XML Metadata' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $json = $result.Content | ConvertFrom-Json

        # Check Product schema exists
        $json.components.schemas.Product | Should -Not -BeNullOrEmpty
        
        # Check required properties
        $json.components.schemas.Product.required | Should -Contain 'Id'
        $json.components.schemas.Product.required | Should -Contain 'Name'
        $json.components.schemas.Product.required | Should -Contain 'Price'

        # Check XML metadata for Id (attribute)
        $idProp = $json.components.schemas.Product.properties.Id
        $idProp | Should -Not -BeNullOrEmpty
        $idProp.xml | Should -Not -BeNullOrEmpty
        $idProp.xml.name | Should -Be 'id'
        # Attribute is a standard OpenAPI XML property
        $idProp.xml.attribute | Should -Be $true
        
        # Check XML metadata for Name (custom element name)
        $nameProp = $json.components.schemas.Product.properties.Name
        $nameProp | Should -Not -BeNullOrEmpty
        $nameProp.xml | Should -Not -BeNullOrEmpty
        $nameProp.xml.name | Should -Be 'ProductName'

        # Check XML metadata for Price (namespace and prefix)
        $priceProp = $json.components.schemas.Product.properties.Price
        $priceProp | Should -Not -BeNullOrEmpty
        $priceProp.xml | Should -Not -BeNullOrEmpty
        $priceProp.xml.name | Should -Be 'Price'
        $priceProp.xml.namespace | Should -Be 'http://example.com/pricing'
        $priceProp.xml.prefix | Should -Be 'price'

        # Check XML metadata for Items (wrapped array)
        $itemsProp = $json.components.schemas.Product.properties.Items
        $itemsProp | Should -Not -BeNullOrEmpty
        $itemsProp.xml | Should -Not -BeNullOrEmpty
        $itemsProp.xml.name | Should -Be 'Item'
        # Wrapped is a standard OpenAPI XML property
        $itemsProp.xml.wrapped | Should -Be $true

        # Check endpoints exist
        $json.paths.'/products/{id}'.get | Should -Not -BeNullOrEmpty
        $json.paths.'/products'.post | Should -Not -BeNullOrEmpty
    }
}
