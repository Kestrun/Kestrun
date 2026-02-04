param()
BeforeAll {
    . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1')
}
Describe 'Example 4.1-Shared-Variables' {
    BeforeAll { $script:instance = Start-ExampleScript -Name '4.1-Shared-Variables.ps1' }
    AfterAll {
        if ($script:instance) {
            # Stop the example script
            Stop-ExampleScript -Instance $script:instance
            # Diagnostic info on failure
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }
    It 'Shared variable routes respond with 200' { Test-ExampleRouteSet -Instance $script:instance }
}
