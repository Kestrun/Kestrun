param()
Describe 'Example 9.9-Content-Negotiation' {
    BeforeAll { . (Join-Path $PSScriptRoot '..\PesterHelpers.ps1'); $script:instance = Start-ExampleScript -Name '9.9-Content-Negotiation.ps1' }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'Content negotiation routes respond with 200' { Test-ExampleRouteSet -Instance $script:instance }
}
