param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.9 Component Header' -Tag 'Tutorial', 'OpenApi', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.9-OpenAPI-Component-Header.ps1'
    }

    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Runtime responses include expected headers' {
        # Create (1st throttle-counted call)
        $createBody = @{
            firstName = 'Jane'
            lastName = 'Doe'
            email = 'jane.doe@example.com'
        } | ConvertTo-Json

        $create = Invoke-WebRequest -Uri "$($script:instance.Url)/users" -Method Post -Body $createBody -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $create.StatusCode | Should -Be 201

        $created = $create.Content | ConvertFrom-Json
        $created.id | Should -BeGreaterThan 0
        $id = [int]$created.id

        $create.Headers['X-Correlation-Id'] | Should -Not -BeNullOrEmpty
        $create.Headers['Location'] | Should -Be "/users/$id"
        $create.Headers['ETag'] | Should -Be ('W/"user-{0}-v1"' -f $id)
        $create.Headers['X-RateLimit-Limit'] | Should -Not -BeNullOrEmpty
        $create.Headers['X-RateLimit-Remaining'] | Should -Not -BeNullOrEmpty
        $create.Headers['X-RateLimit-Reset'] | Should -Not -BeNullOrEmpty

        # Get (2nd throttle-counted call)
        $get = Invoke-WebRequest -Uri "$($script:instance.Url)/users/$id" -Method Get -SkipCertificateCheck -SkipHttpErrorCheck
        $get.StatusCode | Should -Be 200
        $get.Headers['X-Correlation-Id'] | Should -Not -BeNullOrEmpty
        $get.Headers['ETag'] | Should -Not -BeNullOrEmpty

        # Delete (not throttle-counted)
        $delete = Invoke-WebRequest -Uri "$($script:instance.Url)/users/$id" -Method Delete -SkipCertificateCheck -SkipHttpErrorCheck
        $delete.StatusCode | Should -Be 204
        $delete.Headers['X-Correlation-Id'] | Should -Not -BeNullOrEmpty

        # Get after delete (3rd throttle-counted call, should still be allowed)
        $getMissing = Invoke-WebRequest -Uri "$($script:instance.Url)/users/$id" -Method Get -SkipCertificateCheck -SkipHttpErrorCheck
        $getMissing.StatusCode | Should -Be 404
        $getMissing.Headers['X-Error-Code'] | Should -Be 'USER_NOT_FOUND'
    }

    It 'OpenAPI document contains header components' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $json.components.headers.'X-Correlation-Id' | Should -Not -BeNullOrEmpty
        $json.components.headers.Location | Should -Not -BeNullOrEmpty
        $json.components.headers.ETag | Should -Not -BeNullOrEmpty
        $json.components.headers.'X-RateLimit-Limit' | Should -Not -BeNullOrEmpty
        $json.components.headers.'X-RateLimit-Remaining' | Should -Not -BeNullOrEmpty
        $json.components.headers.'X-RateLimit-Reset' | Should -Not -BeNullOrEmpty
        $json.components.headers.'Retry-After' | Should -Not -BeNullOrEmpty
    }

    It 'OpenAPI header components include vendor extensions (x-*)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $remaining = $json.components.headers.'X-RateLimit-Remaining'
        $remaining.'x-kestrun-demo'.exampleRemaining | Should -Be 1
        $remaining.'x-kestrun-demo'.computedPer | Should -Be 'client-ip'
        $remaining.'x-kestrun-demo'.windowSeconds | Should -Be 60

        $reset = $json.components.headers.'X-RateLimit-Reset'
        $reset.'x-kestrun-demo'.resetSeconds | Should -Be 60
        $reset.'x-kestrun-demo'.correlationIdExample | Should -BeLike '????????-????-????-????-????????????'
    }

    It 'OpenAPI document applies response headers via $ref and inline definitions' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        # POST /users (201)
        $post201 = $json.paths.'/users'.post.responses.'201'
        $post201.headers.'X-Correlation-Id'.'$ref' | Should -Be '#/components/headers/X-Correlation-Id'
        $post201.headers.Location.'$ref' | Should -Be '#/components/headers/Location'
        $post201.headers.ETag.'$ref' | Should -Be '#/components/headers/ETag'
        $post201.headers.'X-RateLimit-Limit'.'$ref' | Should -Be '#/components/headers/X-RateLimit-Limit'
        $post201.headers.'X-RateLimit-Remaining'.'$ref' | Should -Be '#/components/headers/X-RateLimit-Remaining'
        $post201.headers.'X-RateLimit-Reset'.'$ref' | Should -Be '#/components/headers/X-RateLimit-Reset'

        # GET /users/{userId} (200)
        $get200 = $json.paths.'/users/{userId}'.get.responses.'200'
        $get200.headers.ETag.'$ref' | Should -Be '#/components/headers/ETag'

        # 429 responses reference Retry-After
        $post429 = $json.paths.'/users'.post.responses.'429'
        $post429.headers.'Retry-After'.'$ref' | Should -Be '#/components/headers/Retry-After'

        # Inline header example (400)
        $post400 = $json.paths.'/users'.post.responses.'400'
        $post400.headers.'X-Error-Code'.schema.type | Should -Be 'string'
    }

    It 'Swagger UI and Redoc UI are available' {
        $swagger = Invoke-WebRequest -Uri "$($script:instance.Url)/docs/swagger" -SkipCertificateCheck -SkipHttpErrorCheck
        $swagger.StatusCode | Should -Be 200
        $swagger.Content | Should -BeLike '*swagger-ui*'

        $redoc = Invoke-WebRequest -Uri "$($script:instance.Url)/docs/redoc" -SkipCertificateCheck -SkipHttpErrorCheck
        $redoc.StatusCode | Should -Be 200
        $redoc.Content | Should -BeLike '*Redoc*'
    }

    It 'OpenAPI v3.0 output matches 10.9 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.0'
    }

    It 'OpenAPI v3.1 output matches 10.9 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.1'
    }

    It 'OpenAPI v3.2 output matches 10.9 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.2'
    }
}

