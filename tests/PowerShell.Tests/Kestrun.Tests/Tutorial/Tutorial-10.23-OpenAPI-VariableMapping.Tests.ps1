param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.23 RFC 6570 Variable Mapping' -Tag 'Tutorial', 'OpenApi', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.23-OpenAPI-VariableMapping.ps1'
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'Simple parameter extraction works' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/users/42" -Method Get -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.id | Should -Be 42
        $json.username | Should -Be 'user42'
        $json.email | Should -Be 'user42@example.com'
    }

    It 'Versioned path parameters work' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/api/v1/users/123" -Method Get -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.id | Should -Be 123
        $json.username | Should -Be 'user123'
        $json.apiVersion | Should -Be 'v1'
    }

    It 'Reserved operator multi-segment path works' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/files/documents/reports/2024/annual.pdf" -Method Get -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.path | Should -Be 'documents/reports/2024/annual.pdf'
        $json.size | Should -Be 1024
    }

    It 'Variable mapping introspection endpoint works' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/mapping/mytemplate/999" -Method Get -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.template | Should -Be '/mapping/{template}/{id}'
        $json.variables.template | Should -Be 'mytemplate'
        $json.variables.id | Should -Be '999'
        $json.rfc6570Compliant | Should -Be $true
    }

    It 'OpenAPI document contains RFC 6570 path expressions' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.2/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json -AsHashtable

        # Simple parameter
        $json.paths['/users/{userId}'] | Should -Not -BeNullOrEmpty

        # RFC6570-style variables (no ':' constraints)
        $json.paths['/api/v{version}/users/{userId}'] | Should -Not -BeNullOrEmpty

        # Reserved operator (multi-segment)
        $hasMultiSegment = $json.paths.ContainsKey('/files/{+path}') -or $json.paths.ContainsKey('/files/{path}')
        $hasMultiSegment | Should -Be $true
    }

    It 'OpenAPI document includes schema components' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.2/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $json.components.schemas.User | Should -Not -BeNullOrEmpty
        $json.components.schemas.User.properties.id | Should -Not -BeNullOrEmpty
        $json.components.schemas.User.properties.username | Should -Not -BeNullOrEmpty

        $json.components.schemas.FileInfo | Should -Not -BeNullOrEmpty
        $json.components.schemas.FileInfo.properties.path | Should -Not -BeNullOrEmpty

        $json.components.schemas.VariableMapping | Should -Not -BeNullOrEmpty
        $json.components.schemas.VariableMapping.properties.template | Should -Not -BeNullOrEmpty
        $json.components.schemas.VariableMapping.properties.variables | Should -Not -BeNullOrEmpty
    }

    It 'OpenAPI document has correct tags' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.2/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $tagNames = $json.tags | ForEach-Object { $_.name }
        $tagNames | Should -Contain 'Users'
        $tagNames | Should -Contain 'Files'
        $tagNames | Should -Contain 'API'
    }

    It 'Swagger UI and Redoc UI are available' {
        $swagger = Invoke-WebRequest -Uri "$($script:instance.Url)/docs/swagger" -SkipCertificateCheck -SkipHttpErrorCheck
        $swagger.StatusCode | Should -Be 200
        $swagger.Content | Should -BeLike '*swagger-ui*'

        $redoc = Invoke-WebRequest -Uri "$($script:instance.Url)/docs/redoc" -SkipCertificateCheck -SkipHttpErrorCheck
        $redoc.StatusCode | Should -Be 200
        $redoc.Content | Should -BeLike '*Redoc*'
    }
}
