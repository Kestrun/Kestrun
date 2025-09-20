param()
Describe 'Example 7.7-Uri-Endpoint' -Tag 'Tutorial', 'Slow' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '7.7-Uri-Endpoint.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'URI endpoint routes respond with 200' { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; Test-ExampleRouteSet -Instance $script:instance }
}
