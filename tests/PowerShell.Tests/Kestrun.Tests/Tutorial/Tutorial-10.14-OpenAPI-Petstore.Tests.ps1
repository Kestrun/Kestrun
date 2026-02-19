param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'OpenAPI Petstore Example' -Tag 'OpenApi', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.14-OpenAPI-PetStore.ps1'
    }

    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'OpenAPI v3.0 output matches Petstore JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.0'
    }

    It 'OpenAPI v3.1 output matches Petstore JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.1'
    }

    It 'OpenAPI v3.2 output matches Petstore JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance -Version 'v3.2'
    }
}
