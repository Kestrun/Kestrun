param()
Describe 'Example 10.4 OpenAPI Component Parameter' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '10.4-OpenAPI-Component-Parameter.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'List Products (GET Default)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/products" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.page | Should -Be 1
        $json.limit | Should -Be 20
        $json.items.Count | Should -Be 5
    }

    It 'List Products (All Parameters)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/products?page=3&limit=30&sortBy=date&category=electronics&minPrice=100&maxPrice=5000" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.page | Should -Be 3
        $json.limit | Should -Be 30
    }

    It 'List Products (GET Paged)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/products?page=2&limit=5" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.page | Should -Be 2
        $json.limit | Should -Be 5
        $json.total | Should -Be 5
    }

    It 'Check OpenAPI Parameters' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $json = $result.Content | ConvertFrom-Json
        $json.components.parameters.page | Should -Not -BeNullOrEmpty
        $json.components.parameters.limit | Should -Not -BeNullOrEmpty
    }
}

