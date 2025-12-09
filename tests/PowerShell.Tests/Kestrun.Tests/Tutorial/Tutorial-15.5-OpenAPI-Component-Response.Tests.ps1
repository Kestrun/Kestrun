param()
Describe 'Example 15.5 OpenAPI Component Response' -Tag 'Tutorial', 'Slow' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '15.5-OpenAPI-Component-Response.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Get Article (GET Success)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/articles/1" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.id | Should -Be 1
        $json.title | Should -Be 'Getting Started with OpenAPI'
    }

    It 'Get Article (GET NotFound)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/articles/999" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 404
        $json = $result.Content | ConvertFrom-Json
        $json.code | Should -Be 'ARTICLE_NOT_FOUND'
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
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $json = $result.Content | ConvertFrom-Json
        $json.components.responses.'SuccessResponses-OK' | Should -Not -BeNullOrEmpty
        $json.components.responses.'ErrorResponses-NotFound' | Should -Not -BeNullOrEmpty
    }
}
