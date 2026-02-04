param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}

Describe 'Example 9.10-Low-Level-Response' {
    BeforeAll { $script:instance = Start-ExampleScript -Name '9.10-Low-Level-Response.ps1' }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }
    
    It 'Low-level response routes respond with 200' { Test-ExampleRouteSet -Instance $script:instance }
}
