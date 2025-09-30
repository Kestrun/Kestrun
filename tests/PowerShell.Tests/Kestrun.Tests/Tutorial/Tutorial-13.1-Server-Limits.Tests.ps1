param()
Describe 'Example 13.1-Server-Limits' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '13.1-Server-Limits.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Server limit example routes respond with 200' { Test-ExampleRouteSet -Instance $script:instance }
}
