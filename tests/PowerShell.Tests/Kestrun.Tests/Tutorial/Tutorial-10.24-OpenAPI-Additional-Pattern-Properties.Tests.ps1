param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.24 OpenAPI AdditionalProperties + PatternProperties' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.24-OpenAPI-Additional-Pattern-Properties.ps1'
    }
    AfterAll {
        if ($script:instance) {
            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Get inventory counts (GET)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/inventory/counts" -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $json.available | Should -Be 120
        $json.reserved | Should -Be 8
        $json.backorder | Should -Be 3
    }

    It 'Get feature flags (GET)' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/features/flags" -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $json.betaPricing | Should -BeTrue
        $json.allowBackorder | Should -BeFalse
    }

    It 'Update catalog (POST)' {
        $body = @{
            itemId = 'SKU-1001'
            inventory = @{ available = 120; reserved = 8; backorder = 3 }
            flags = @{ betaPricing = $true; allowBackorder = $false }
            source = 'batch-1'
        } | ConvertTo-Json -Depth 10

        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/catalog/update" -Method Post `
            -Body $body -ContentType 'application/json' -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $json.itemId | Should -Be 'SKU-1001'
        $json.inventory.available | Should -Be 120
        $json.flags.betaPricing | Should -BeTrue
        $json.updatedAt | Should -Not -BeNullOrEmpty
    }

    It 'Check OpenAPI schemas' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.1/openapi.json" -Headers @{ Accept = 'application/json' } -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $json.components.schemas.InventoryCounts | Should -Not -BeNullOrEmpty
        $json.components.schemas.InventoryCounts.patternProperties.'^[a-z][a-z0-9_]*$' | Should -Not -BeNullOrEmpty
        $json.components.schemas.InventoryCounts.patternProperties.'^[a-z][a-z0-9_]*$'.type | Should -Be 'integer'

        $json.components.schemas.FeatureFlags | Should -Not -BeNullOrEmpty
        $featureFlagsSchema = $json.components.schemas.FeatureFlags
        $featureFlagsAdditionalProperties = $featureFlagsSchema.PSObject.Properties['additionalProperties']
        if ($null -ne $featureFlagsAdditionalProperties) {
            if ($featureFlagsSchema.additionalProperties -is [bool]) {
                $featureFlagsSchema.additionalProperties | Should -BeTrue
            }
            else {
                $featureFlagsSchema.additionalProperties.type | Should -Be 'boolean'
            }
        }
        else {
            $featureFlagsSchema.type | Should -Be 'object'
        }

        $json.components.schemas.CatalogUpdateRequest | Should -Not -BeNullOrEmpty
        $json.components.schemas.CatalogUpdateRequest.additionalProperties.type | Should -Be 'string'

        $json.paths.'/inventory/counts'.get | Should -Not -BeNullOrEmpty
        $json.paths.'/features/flags'.get | Should -Not -BeNullOrEmpty
        $json.paths.'/catalog/update'.post | Should -Not -BeNullOrEmpty
    }
}
