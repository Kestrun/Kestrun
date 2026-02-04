param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.5 OpenAPI Component Response' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.5-OpenAPI-Component-Response.ps1'
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Get Article (GET Success)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/articles/1" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.id | Should -Be 1
        $json.title | Should -Be 'Getting Started with OpenAPI'
    }

    It 'Get Article (GET NotFound)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/articles/-2" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 400
        $json = $result.Content | ConvertFrom-Json
        $json.code | Should -Be 'INVALID_ID'
    }

    It 'Create Article (POST)' {
        $body = @{
            title = 'New Article'
            content = 'Content here'
            author = 'Me'
        } | ConvertTo-Json

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/articles" -Method Post -Body $body -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 201
        $json = $result.Content | ConvertFrom-Json
        $json.message | Should -Be 'Article created successfully'
    }

    It 'Check OpenAPI Responses' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $json = $result.Content | ConvertFrom-Json
        $json.components.responses.OK | Should -Not -BeNullOrEmpty
        $json.components.responses.NotFound | Should -Not -BeNullOrEmpty
    }

    It 'Check OpenAPI Response Extensions' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $json = $result.Content | ConvertFrom-Json

        $json.components.responses.OK.'x-kestrun-demo'.kind | Should -Be 'success'
        $json.components.responses.OK.'x-kestrun-demo'.stability | Should -Be 'beta'
        $json.components.responses.OK.'x-kestrun-demo'.headers | Should -Contain 'X-Correlation-Id'
        $json.components.responses.OK.'x-kestrun-demo'.links | Should -Contain 'get'
        $json.components.responses.OK.'x-kestrun-demo'.links | Should -Contain 'delete'
    }

    It 'OpenAPI output matches 10.5 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance
    }
}

