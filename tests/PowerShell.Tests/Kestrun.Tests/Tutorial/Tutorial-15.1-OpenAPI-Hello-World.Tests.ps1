param()
Describe 'Example 15.1 OpenAPI Hello World' -Tag 'Tutorial', 'Slow' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1');
        $script:instance = Start-ExampleScript -Name '15.1-OpenAPI-Hello-World.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Get greeting' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/greeting" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $result.Content | Should -Be 'Hello, World!'
    }

    It 'Get OpenAPI JSON' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.openapi | Should -BeLike '3.*'
        $json.info.title | Should -Be 'Hello World API'
        $json.paths.'/greeting'.get | Should -Not -BeNullOrEmpty
    }

    It 'Get Swagger UI' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/docs/swagger" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $result.Content | Should -BeLike '*swagger-ui*'
    }

    It 'Get Redoc UI' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/docs/redoc" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $result.Content | Should -BeLike '*Redoc*'
    }
}
