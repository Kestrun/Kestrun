param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 9.9-Content-Negotiation' {
    BeforeAll { $script:instance = Start-ExampleScript -Name '9.9-Content-Negotiation.ps1' }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }
    
    It 'Content negotiation routes respond with 200' { Test-ExampleRouteSet -Instance $script:instance }
}
