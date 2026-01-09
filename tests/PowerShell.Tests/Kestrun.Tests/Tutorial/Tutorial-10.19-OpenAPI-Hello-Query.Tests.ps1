param()
Describe 'Example 10.19 OpenAPI HTTP QUERY Product Search' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '10.19-OpenAPI-Hello-Query.ps1'
    }
    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'QUERY products with JSON filters and pagination (JSON accept)' {
        $body = @{ q = 'lap'; category = 'electronics'; minPrice = 800; inStock = $true } | ConvertTo-Json -Depth 5
        $url = "$($script:instance.Url)/v1/products/search?page=1&pageSize=2"
        $result = Invoke-WebRequest -Uri $url -CustomMethod 'QUERY' -ContentType 'application/json' -Body $body -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.page | Should -Be 1
        $json.pageSize | Should -Be 2
        ($json.total -ge 1) | Should -BeTrue
        @($json.items).Count | Should -BeLessOrEqual 2
        if (@($json.items).Count -gt 0) { @($json.items)[0].name | Should -BeLike 'Laptop*' }
    }

    It 'QUERY products with YAML negotiation' {
        $body = @{ q = 'lap'; category = 'electronics'; minPrice = 800; inStock = $true } | ConvertTo-Json -Depth 5
        $url = "$($script:instance.Url)/v1/products/search?page=1&pageSize=2"
        $result = Invoke-WebRequest -Uri $url -CustomMethod 'QUERY' -ContentType 'application/json' -Body $body -Headers @{ Accept = 'application/yaml' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        # Some formats may return byte arrays; normalize to string for basic assertions
        $content = $result.Content

        $yaml = if ($content -is [byte[]]) {
            # convert byte[] to pscustomobject
            ConvertFrom-KrYaml -YamlBytes $content
        } else {
            ConvertFrom-KrYaml -Yaml $content
        }
        $yaml.page | Should -Be 1
        $yaml.pageSize | Should -Be 2
        ($yaml.total -ge 1) | Should -BeTrue
        @($yaml.items).Count | Should -BeLessOrEqual 2
        if (@($yaml.items).Count -gt 0) { @($yaml.items)[0].name | Should -BeLike 'Laptop*' }
    }

    It 'OpenAPI 3.2 JSON contains QUERY operation' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.2/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.openapi | Should -BeLike '3.2*'
        $json.paths.'/v1/products/search'.query | Should -Not -BeNullOrEmpty
        $json.components.parameters.page | Should -Not -BeNullOrEmpty
        $json.components.parameters.pageSize | Should -Not -BeNullOrEmpty
        $json.components.schemas.Product | Should -Not -BeNullOrEmpty
        $json.components.schemas.ProductSearchRequest | Should -Not -BeNullOrEmpty
    }

    It 'Swagger UI is available' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/docs/swagger" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $result.Content | Should -BeLike '*swagger-ui*'
    }

    It 'ReDoc UI is available' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/docs/redoc" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $result.Content | Should -BeLike '*Redoc*'
    }
}
