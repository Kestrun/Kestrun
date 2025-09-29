param()
Describe 'Example 9.8-Caching' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '9.8-Caching.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Caching routes respond with 200' { Test-ExampleRouteSet -Instance $script:instance }
}
