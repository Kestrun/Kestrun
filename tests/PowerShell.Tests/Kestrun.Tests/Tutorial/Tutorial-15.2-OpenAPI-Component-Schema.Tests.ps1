param()
Describe 'Example 15.2 OpenAPI Component Schema' -Tag 'Tutorial', 'Slow' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '15.2-OpenAPI-Component-Schema.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'Create User (POST)' {
        $body = @{
            firstName = 'Jane'
            lastName = 'Doe'
            email = 'jane.doe@example.com'
            age = 25
        } | ConvertTo-Json

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/users" -Method Post -Body $body -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 201
        $json = $result.Content | ConvertFrom-Json
        $json.firstName | Should -Be 'Jane'
        $json.id | Should -Be 1
    }

    It 'Create User Invalid (POST)' {
        $body = @{
            firstName = 'Jane'
            # Missing lastName and email
        } | ConvertTo-Json

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/users" -Method Post -Body $body -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 400
    }

    It 'Get User (GET)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/users/1" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json
        $json.id | Should -Be 1
        $json.firstName | Should -Be 'John'
    }

    It 'Check OpenAPI Schemas' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $json = $result.Content | ConvertFrom-Json
        $json.components.schemas.CreateUserRequest | Should -Not -BeNullOrEmpty
        $json.components.schemas.UserResponse | Should -Not -BeNullOrEmpty
        $json.components.schemas.CreateUserRequest.required | Should -Contain 'firstName'
    }
}
