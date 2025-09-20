param()
Describe 'Example 7.2-Multiple-Listeners' {
    BeforeAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; $script:instance = Start-ExampleScript -Name '7.2-Multiple-Listeners.ps1' }
    AfterAll { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }
    It 'At least primary listener routes respond with 200' { . "$PSScriptRoot/TutorialExampleTestHelper.ps1"; Test-ExampleRouteSet -Instance $script:instance }
}
