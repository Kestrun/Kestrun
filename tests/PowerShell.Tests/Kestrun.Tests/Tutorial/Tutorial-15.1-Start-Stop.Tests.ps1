param()
Describe 'Example 15.1-Start-Stop' -Tag 'Tutorial', 'Slow' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '15.1-Start-Stop.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Start/Stop routes respond with 200' { Test-ExampleRouteSet -Instance $script:instance }
}
