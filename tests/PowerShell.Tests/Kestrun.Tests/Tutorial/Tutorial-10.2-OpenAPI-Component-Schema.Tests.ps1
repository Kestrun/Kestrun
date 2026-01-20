param()
Describe 'Example 10.2 OpenAPI Component Schema' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
        $script:instance = Start-ExampleScript -Name '10.2-OpenAPI-Component-Schema.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }

    It 'List Employees (GET)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/employees" -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $json | Should -Not -BeNullOrEmpty
        $json.Count | Should -BeGreaterThan 0
        $json[0].employeeId | Should -Not -BeNullOrEmpty
        $json[0].firstName | Should -Not -BeNullOrEmpty
        $json[0].email | Should -Not -BeNullOrEmpty
    }

    It 'Purchase Tickets (POST)' {
        $body = @{
            customer = @{
                firstName = 'Jane'
                lastName = 'Doe'
                email = 'jane.doe@example.com'
            }
            items = @(
                @{ ticketType = 'general'; quantity = 2; unitPrice = 25.0 }
            )
            visitDates = @('2026-01-14')
            note = 'Please email the receipt.'
        } | ConvertTo-Json -Depth 10

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/tickets/purchase" -Method Post `
            -Body $body -ContentType 'application/json' -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 201
        $json = $result.Content | ConvertFrom-Json
        $json.ticketId | Should -Not -BeNullOrEmpty
        [double]$json.total | Should -BeGreaterThan 0
        $json.createdAt | Should -Not -BeNullOrEmpty
        $json.items.Count | Should -Be 1
    }

    It 'Purchase Tickets Invalid (POST)' {
        $body = @{
            customer = @{
                firstName = 'Jane'
                lastName = 'Doe'
                # Missing email
            }
            items = @(
                @{ ticketType = 'general'; quantity = 1; unitPrice = 25.0 }
            )
            visitDates = @('2026-01-14')
        } | ConvertTo-Json -Depth 10

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/tickets/purchase" -Method Post `
            -Body $body -ContentType 'application/json' -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 400
        $json = $result.Content | ConvertFrom-Json
        $json.code | Should -Be 400
        $json.message | Should -Match 'customer\.email'
    }

    It 'Check OpenAPI Schemas' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $json = $result.Content | ConvertFrom-Json

        $json.components.schemas.Date | Should -Not -BeNullOrEmpty
        $json.components.schemas.Money | Should -Not -BeNullOrEmpty
        $json.components.schemas.EmployeeId | Should -Not -BeNullOrEmpty

        $json.components.schemas.EmployeeResponse | Should -Not -BeNullOrEmpty
        $json.components.schemas.EmployeeResponse.allOf[1].required | Should -Contain 'employeeId'
        $json.components.schemas.EmployeeResponse.allOf[1].required | Should -Contain 'createdAt'

        $json.components.schemas.EmployeeList | Should -Not -BeNullOrEmpty
        $json.components.schemas.PurchaseRequest | Should -Not -BeNullOrEmpty
        $json.components.schemas.PurchaseRequest.required | Should -Contain 'customer'
        $json.components.schemas.PurchaseRequest.required | Should -Contain 'items'
        $json.components.schemas.PurchaseRequest.required | Should -Contain 'visitDates'
        $json.components.schemas.PurchaseResponse | Should -Not -BeNullOrEmpty

        # Schema vendor extensions (x-*)
        $json.components.schemas.Address | Should -Not -BeNullOrEmpty
        $json.components.schemas.Address.'x-badges' | Should -Not -BeNullOrEmpty
        $json.components.schemas.Address.'x-badges'[0].name | Should -Be 'Beta'
        $json.components.schemas.Address.'x-badges'[1].name | Should -Be 'PII'
        $json.components.schemas.Address.'x-kestrun-demo'.owner | Should -Be 'docs'
        $json.components.schemas.Address.'x-kestrun-demo'.stability | Should -Be 'beta'
        $json.components.schemas.Address.'x-kestrun-demo'.containsPii | Should -BeTrue

        # Enum schema component validation
        $json.components.schemas.TicketType | Should -Not -BeNullOrEmpty
        $json.components.schemas.TicketType.type | Should -Be 'string'
        $json.components.schemas.TicketType.enum | Should -Not -BeNullOrEmpty
        $json.components.schemas.TicketType.enum | Should -Contain 'general'
        $json.components.schemas.TicketType.enum | Should -Contain 'event'

        # Verify LineItem.ticketType uses $ref to TicketType enum
        $json.components.schemas.LineItem | Should -Not -BeNullOrEmpty
        $json.components.schemas.LineItem.properties.ticketType.'$ref' | Should -Be '#/components/schemas/TicketType'

        # Verify PurchaseRequest.preferredTicketType uses anyOf for nullable enum
        $json.components.schemas.PurchaseRequest | Should -Not -BeNullOrEmpty
        $json.components.schemas.PurchaseRequest.properties.preferredTicketType | Should -Not -BeNullOrEmpty
        # Nullable enum should produce anyOf with $ref and null
        $json.components.schemas.PurchaseRequest.properties.preferredTicketType.anyOf | Should -Not -BeNullOrEmpty
        $json.components.schemas.PurchaseRequest.properties.preferredTicketType.anyOf.Count | Should -Be 2
        # First item should be the enum reference
        $json.components.schemas.PurchaseRequest.properties.preferredTicketType.anyOf[0].'$ref' | Should -Be '#/components/schemas/TicketType'
        # Second item should be null type
        $json.components.schemas.PurchaseRequest.properties.preferredTicketType.anyOf[1].type | Should -Be 'null'
        # Verify it's not in required properties (nullable/optional)
        $json.components.schemas.PurchaseRequest.required | Should -Not -Contain 'preferredTicketType'

        $json.paths.'/employees'.get | Should -Not -BeNullOrEmpty
        $json.paths.'/tickets/purchase'.post | Should -Not -BeNullOrEmpty
    }

    It 'OpenAPI output matches 10.2 fixture JSON' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200

        $actualNormalized = Get-NormalizedJson $result.Content
        $expectedPath = Join-Path -Path (Get-TutorialExamplesDirectory) -ChildPath 'Assets' `
            -AdditionalChildPath 'OpenAPI', "$($script:instance.BaseName).json"

        $expectedContent = Get-Content -Path $expectedPath -Raw
        $expectedNormalized = Get-NormalizedJson $expectedContent

        $actualNormalized | Should -Be $expectedNormalized
    }
}
