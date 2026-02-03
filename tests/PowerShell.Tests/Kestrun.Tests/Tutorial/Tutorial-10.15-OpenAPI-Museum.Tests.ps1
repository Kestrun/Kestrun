param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'OpenAPI Museum Example' -Tag 'OpenApi', 'Slow' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '10.15-OpenAPI-Museum.ps1'
    }

    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'OpenAPI output matches Museum JSON' {
        Test-OpenApiDocumentMatchesExpected -Instance $script:instance
    }
}
