param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 13.1-Server-Limits' {
    BeforeAll {
        $script:instance = Start-ExampleScript -Name '13.1-Server-Limits.ps1'
    }
    AfterAll { if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }
    It 'Server limit example routes respond with 200' { Test-ExampleRouteSet -Instance $script:instance }
}
