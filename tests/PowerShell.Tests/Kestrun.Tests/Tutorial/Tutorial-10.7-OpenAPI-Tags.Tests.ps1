param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 10.7 OpenAPI Tags' -Tag 'OpenApi', 'Tutorial', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.7-OpenAPI-Tags.ps1'
        <#
            .SYNOPSIS
                Retrieves the parent tag name from a tag object.
            .PARAMETER Tag
                The tag object from which to extract the parent name.
            .OUTPUTS
                The name of the parent tag, or $null if no parent exists.
        #>
        function Get-TagParentName {

            param([Parameter(Mandatory)][object]$Tag)
            $p = $Tag.parent
            if ($null -eq $p) { return $null }
            if ($p -is [string]) { return $p }
            if ($p.PSObject.Properties.Name -contains 'name') { return $p.name }
            if ($p.PSObject.Properties.Name -contains '$ref') { return ($p.'$ref' -split '/')[-1] }
            return $null
        }
    }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'Check OpenAPI Tags' {
        $result = Invoke-WebRequest -Uri "$($script:instance.Url)/openapi/v3.2/openapi.json" -SkipCertificateCheck -SkipHttpErrorCheck
        $result.StatusCode | Should -Be 200
        $json = $result.Content | ConvertFrom-Json

        $json.openapi | Should -Match '^3\.2'

        # Document-level externalDocs (Add-KrOpenApiExternalDoc)
        $json.externalDocs | Should -Not -BeNullOrEmpty
        $json.externalDocs.description | Should -Be 'API portal'
        $json.externalDocs.url | Should -Be 'https://example.com/api-portal'
        $json.externalDocs.'x-docType' | Should -Be 'portal'
        $json.externalDocs.'x-audience' | Should -Be 'internal'

        $tags = $json.tags
        $tags | Should -Not -BeNullOrEmpty

        $opsTag = $tags | Where-Object { $_.name -eq 'operations' }
        $opsTag | Should -Not -BeNullOrEmpty
        $opsTag.kind | Should -Be 'category'
        $opsTag.'x-displayName' | Should -Be 'Operations'
        $opsTag.'x-icon' | Should -Be 'tools'

        $ordersTag = $tags | Where-Object { $_.name -eq 'orders' }
        $ordersTag | Should -Not -BeNullOrEmpty
        $ordersTag.description | Should -Be 'Order operations'
        $ordersTag.kind | Should -Be 'resource'
        (Get-TagParentName -Tag $ordersTag) | Should -Be 'operations'
        $ordersTag.'x-displayName' | Should -Be 'Orders'
        $ordersTag.'x-releaseStage' | Should -Be 'beta'
        $ordersTag.externalDocs | Should -Not -BeNullOrEmpty
        $ordersTag.externalDocs.description | Should -Be 'Order docs'
        $ordersTag.externalDocs.url | Should -Be 'https://example.com/orders'
        $ordersTag.externalDocs.'x-docType' | Should -Be 'reference'
        $ordersTag.externalDocs.'x-audience' | Should -Be 'public'

        $ordersReadTag = $tags | Where-Object { $_.name -eq 'orders.read' }
        $ordersReadTag | Should -Not -BeNullOrEmpty
        $ordersReadTag.kind | Should -Be 'operation'
        (Get-TagParentName -Tag $ordersReadTag) | Should -Be 'orders'
        $ordersReadTag.'x-scope' | Should -Be 'orders:read'

        $healthTag = $tags | Where-Object { $_.name -eq 'health' }
        $healthTag | Should -Not -BeNullOrEmpty
        $healthTag.kind | Should -Be 'resource'
        (Get-TagParentName -Tag $healthTag) | Should -Be 'operations'
    }

    It 'OpenAPI v3.0 output matches 10.7 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.0'
    }

    It 'OpenAPI v3.1 output matches 10.7 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.1'
    }

    It 'OpenAPI v3.2 output matches 10.7 fixture JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.2'
    }
}
