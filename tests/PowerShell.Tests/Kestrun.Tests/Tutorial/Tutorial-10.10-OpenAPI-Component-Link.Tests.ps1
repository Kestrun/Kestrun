param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.10 Component Link' -Tag 'Tutorial', 'OpenApi', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.10-OpenAPI-Component-Link.ps1'
    }

    AfterAll {
        if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
    }

    It 'CRUD user operations behave as expected' {
        # Create
        $createBody = @{
            firstName = 'Jane'
            lastName = 'Doe'
            email = 'jane.doe@example.com'
        } | ConvertTo-Json

        $create = Invoke-WebRequest -Uri "$($script:instance.Url)/users" -Method Post -Body $createBody -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $create.StatusCode | Should -Be 201
        $created = $create.Content | ConvertFrom-Json
        $created.id | Should -BeGreaterThan 0
        $created.user.firstName | Should -Be 'Jane'
        $created.user.email | Should -Be 'jane.doe@example.com'

        $id = [int]$created.id

        # Get
        $get = Invoke-WebRequest -Uri "$($script:instance.Url)/users/$id" -Method Get -SkipCertificateCheck -SkipHttpErrorCheck
        $get.StatusCode | Should -Be 200
        ($get.Content | ConvertFrom-Json).user.lastName | Should -Be 'Doe'

        # Update
        $updateBody = @{
            firstName = 'Janet'
            lastName = 'Doe'
            email = 'janet.doe@example.com'
        } | ConvertTo-Json

        $update = Invoke-WebRequest -Uri "$($script:instance.Url)/users/$id" -Method Put -Body $updateBody -ContentType 'application/json' -SkipCertificateCheck -SkipHttpErrorCheck
        $update.StatusCode | Should -Be 200
        $updated = $update.Content | ConvertFrom-Json
        $updated.id | Should -Be $id
        $updated.user.firstName | Should -Be 'Janet'

        # Delete
        $delete = Invoke-WebRequest -Uri "$($script:instance.Url)/users/$id" -Method Delete -SkipCertificateCheck -SkipHttpErrorCheck
        $delete.StatusCode | Should -Be 204

        # Get after delete
        $getMissing = Invoke-WebRequest -Uri "$($script:instance.Url)/users/$id" -Method Get -SkipCertificateCheck -SkipHttpErrorCheck
        $getMissing.StatusCode | Should -Be 404
    }

    It 'OpenAPI document contains link components' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $json.components.links.GetUserLink | Should -Not -BeNullOrEmpty
        $json.components.links.UpdateUserLink | Should -Not -BeNullOrEmpty
        $json.components.links.DeleteUserLink | Should -Not -BeNullOrEmpty

        $json.components.links.GetUserLink.operationId | Should -Be 'getUser'
        $json.components.links.GetUserLink.parameters.userId | Should -Be '$response.body#/id'

        $json.components.links.UpdateUserLink.operationId | Should -Be 'updateUser'
        $json.components.links.UpdateUserLink.parameters.userId | Should -Be '$response.body#/id'
        $json.components.links.UpdateUserLink.requestBody | Should -Be '$response.body#/user'

        $json.components.links.DeleteUserLink.operationId | Should -Be 'deleteUser'
        $json.components.links.DeleteUserLink.parameters.userId | Should -Be '$response.body#/id'
    }

    It 'OpenAPI link components include vendor extensions (x-*)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $getLink = $json.components.links.GetUserLink
        $getLink.'x-kestrun-demo'.relation | Should -Be 'get'
        $getLink.'x-kestrun-demo'.targetOperation | Should -Be 'getUser'
        $getLink.'x-kestrun-demo'.parameterSource | Should -Be '$response.body#/id'

        $updateLink = $json.components.links.UpdateUserLink
        $updateLink.'x-kestrun-demo'.relation | Should -Be 'update'
        $updateLink.'x-kestrun-demo'.targetOperation | Should -Be 'updateUser'
        $updateLink.'x-kestrun-demo'.parameterSource | Should -Be '$response.body#/id'
        $updateLink.'x-kestrun-demo'.requestBodySource | Should -Be '$response.body#/user'

        $deleteLink = $json.components.links.DeleteUserLink
        $deleteLink.'x-kestrun-demo'.relation | Should -Be 'delete'
        $deleteLink.'x-kestrun-demo'.targetOperation | Should -Be 'deleteUser'
        $deleteLink.'x-kestrun-demo'.parameterSource | Should -Be '$response.body#/id'
    }

    It 'OpenAPI document applies links to responses via $ref' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        # POST /users (201)
        $postLinks = $json.paths.'/users'.post.responses.'201'.links
        $postLinks.get.'$ref' | Should -Be '#/components/links/GetUserLink'
        $postLinks.update.'$ref' | Should -Be '#/components/links/UpdateUserLink'
        $postLinks.delete.'$ref' | Should -Be '#/components/links/DeleteUserLink'

        # GET /users/{userId} (200)
        $getLinks = $json.paths.'/users/{userId}'.get.responses.'200'.links
        $getLinks.update.'$ref' | Should -Be '#/components/links/UpdateUserLink'
        $getLinks.delete.'$ref' | Should -Be '#/components/links/DeleteUserLink'

        # PUT /users/{userId} (200)
        $putLinks = $json.paths.'/users/{userId}'.put.responses.'200'.links
        $putLinks.get.'$ref' | Should -Be '#/components/links/GetUserLink'
        $putLinks.delete.'$ref' | Should -Be '#/components/links/DeleteUserLink'
    }

    It 'Swagger UI and Redoc UI are available' {
        $swagger = Invoke-WebRequest -Uri "$($script:instance.Url)/docs/swagger" -SkipCertificateCheck -SkipHttpErrorCheck
        $swagger.StatusCode | Should -Be 200
        $swagger.Content | Should -BeLike '*swagger-ui*'

        $redoc = Invoke-WebRequest -Uri "$($script:instance.Url)/docs/redoc" -SkipCertificateCheck -SkipHttpErrorCheck
        $redoc.StatusCode | Should -Be 200
        $redoc.Content | Should -BeLike '*Redoc*'
    }

    It 'OpenAPI output matches 10.10 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance
    }
}

