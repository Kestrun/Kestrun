param()
Describe 'Example 15.4 OpenAPI Component Parameter' -Tag 'Tutorial', 'Slow' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '15.4-OpenAPI-Component-Parameter.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'List Products (GET Default)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/products" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.page | Should -Be 1
        $json.limit | Should -Be 10
        $json.items.Count | Should -BeGreaterThan 0
    }

    It 'List Products (GET Paged)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/products?page=2&limit=5" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.page | Should -Be 2
        $json.limit | Should -Be 5
    }

    It 'Check OpenAPI Parameters' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $json = $result.Content | ConvertFrom-Json
        $json.components.parameters.PaginationParameters_page | Should -Not -BeNullOrEmpty
        $json.components.parameters.PaginationParameters_limit | Should -Not -BeNullOrEmpty
    }
}
