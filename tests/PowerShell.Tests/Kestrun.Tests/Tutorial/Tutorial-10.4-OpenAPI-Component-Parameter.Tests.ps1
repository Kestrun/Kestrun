param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.4 OpenAPI Component Parameter' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.4-OpenAPI-Component-Parameter.ps1'
    }
    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'List Products (GET Default)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/v1/products" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.page | Should -Be 1
        $json.limit | Should -Be 20
        $json.items.Count | Should -Be 5
    }

    It 'List Products (All Parameters)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/v1/products?page=3&limit=30&sortBy=price&category=electronics&minPrice=100&maxPrice=5000" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.page | Should -Be 3
        $json.limit | Should -Be 30
    }

    It 'List Products (GET Paged)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/v1/products?page=2&limit=5" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.page | Should -Be 2
        $json.limit | Should -Be 5
        $json.total | Should -Be 5
    }

    It 'Get Product by Id (GET)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/v1/products/1" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.id | Should -Be 1
        $json.name | Should -Not -BeNullOrEmpty
    }

    It 'Get Product by Id (GET Not Found)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/v1/products/999999" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 404
        $json = $result.Content | ConvertFrom-Json
        $json.message | Should -Be 'Product not found'
    }

    It 'Create Product (POST DryRun)' {
        $body = @{
            name     = 'USB-C Dock'
            category = 'electronics'
            price    = 159.99
            tags     = @('usb-c', 'dock')
        } | ConvertTo-Json -Compress

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/v1/products?dryRun=true" -Method Post -ContentType 'application/json' -Body $body -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 201
        $json = $result.Content | ConvertFrom-Json
        $json.id | Should -Be 0
        $json.name | Should -Be 'USB-C Dock'
    }

    It 'Create Product (POST Invalid Body)' {
        $body = @{
            name     = ''
            category = 'electronics'
            price    = 159.99
        } | ConvertTo-Json -Compress

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/v1/products?dryRun=true" -Method Post -ContentType 'application/json' -Body $body -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 400
        $json = $result.Content | ConvertFrom-Json
        $json.message | Should -Be 'name is required'
    }

    It 'List Categories (GET Default includeCounts=true)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/v1/categories" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $json.items | Should -Not -BeNullOrEmpty
        ($json.items | Measure-Object).Count | Should -BeGreaterThan 1
        ($json.items.count | Measure-Object -Maximum).Maximum | Should -BeGreaterThan 0
    }

    It 'List Categories (GET includeCounts=false)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/v1/categories?includeCounts=false" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $json.items | Should -Not -BeNullOrEmpty
        foreach ($item in $json.items) {
            $item.count | Should -Be 0
        }
    }

    It 'Swagger UI route responds' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/docs/swagger" -MaximumRedirection 5 -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -BeIn @(200, 301, 302)
    }

    It 'ReDoc route responds' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/docs/redoc" -MaximumRedirection 5 -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -BeIn @(200, 301, 302)
    }

    It 'Check OpenAPI Parameters' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $json = $result.Content | ConvertFrom-Json
        $json.components.parameters.page | Should -Not -BeNullOrEmpty
        $json.components.parameters.limit | Should -Not -BeNullOrEmpty
    }

    It 'OpenAPI includes all example paths' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200

        $json = $result.Content | ConvertFrom-Json

        $json.paths.'/v1/products' | Should -Not -BeNullOrEmpty
        $json.paths.'/v1/products'.get | Should -Not -BeNullOrEmpty
        $json.paths.'/v1/products'.post | Should -Not -BeNullOrEmpty

        $json.paths.'/v1/products/{productId}' | Should -Not -BeNullOrEmpty
        $json.paths.'/v1/products/{productId}'.get | Should -Not -BeNullOrEmpty

        $json.paths.'/v1/categories' | Should -Not -BeNullOrEmpty
        $json.paths.'/v1/categories'.get | Should -Not -BeNullOrEmpty
    }

    It 'OpenAPI output matches 10.4 fixture JSON' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200

        $actualNormalized = Get-NormalizedJson $result.Content
        $expectedPath = Join-Path -Path (Get-TutorialExamplesDirectory) -ChildPath 'Assets' -AdditionalChildPath 'OpenAPI', '10.4-Parameters.json'

        $expectedContent = Get-Content -Path $expectedPath -Raw
        $expectedNormalized = Get-NormalizedJson $expectedContent

        $actualNormalized | Should -Be $expectedNormalized
    }
}
