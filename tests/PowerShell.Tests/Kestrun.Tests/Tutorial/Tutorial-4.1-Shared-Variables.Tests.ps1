param()
Describe 'Example 4.1-Shared-Variables' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '4.1-Shared-Variables.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Shared variable routes respond with 200' { Test-ExampleRouteSet -Instance $script:instance }
}
